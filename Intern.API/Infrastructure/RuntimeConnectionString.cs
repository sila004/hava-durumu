using Npgsql;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.RegularExpressions;

namespace Intern.API.Infrastructure;

/// <summary>Npgsql bağlantı dizesi, Neon DATABASE_URL adresi veya ayrı DB_* ayarlarını çözer.</summary>
public static class RuntimeConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var existing = new[] { configuration["CONNECTION_STRING"], configuration["DATABASE_URL"],
            configuration.GetConnectionString("DefaultConnection") }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(existing))
            return ParseConnectionValue(existing);

        var host = configuration["DB_HOST"];
        var database = configuration["DB_NAME"];
        var username = configuration["DB_USERNAME"];
        var password = configuration["DB_PASSWORD"];

        var missing = new[]
            {
                (Name: "DB_HOST", Value: host),
                (Name: "DB_NAME", Value: database),
                (Name: "DB_USERNAME", Value: username),
                (Name: "DB_PASSWORD", Value: password)
            }
            .Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Name)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Database configuration is incomplete. Missing: {string.Join(", ", missing)}.");
        }

        var port = int.TryParse(configuration["DB_PORT"], out var configuredPort)
            ? configuredPort
            : 5432;
        var commandTimeout = int.TryParse(configuration["DB_COMMAND_TIMEOUT_SECONDS"], out var configuredTimeout)
            ? configuredTimeout
            : 300;

        return new NpgsqlConnectionStringBuilder
        {
            Host = host!,
            Port = port,
            Database = database!,
            Username = username!,
            Password = password!,
            CommandTimeout = commandTimeout
        }.ConnectionString;
    }

    private static string ParseConnectionValue(string value)
    {
        try
        {
            value = NormalizeCopiedValue(value);
            if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                return FromPostgresUrl(value);

            // Geçersiz metin EF migration'a ulaşmadan yakalanır. Alttaki ayrıştırıcı
            // hatası, kopyalanmış parola/URL içerebileceğinden dışarı taşınmaz.
            var parsed = new NpgsqlConnectionStringBuilder(value);
            if (string.IsNullOrWhiteSpace(parsed.Host)) throw new FormatException();
            return value;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("Veritabanı bağlantı bilgisi okunamadı. Render Environment bölümündeki DATABASE_URL alanına Neon'dan kopyalanan gerçek postgresql:// adresini ekleyin.");
        }
    }

    private static string NormalizeCopiedValue(string value)
    {
        value = Unquote(value);
        var lines = value.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#')).ToArray();
        if (lines.Length > 1)
        {
            // Neon "Copy connection strings" doğrudan ve havuzlu adresi birlikte
            // verebilir. Açılış migration'ları için doğrudan adres tercih edilir.
            var entries = lines.Select(MatchAssignment).ToArray();
            if (entries.Any(entry => !entry.Success)) throw new FormatException();
            var direct = entries.Where(entry => entry.Groups[1].Value.Equals("DATABASE_URL", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (direct.Length != 1) throw new FormatException();
            value = direct[0].Groups[2].Value;
        }
        else if (lines.Length == 1) value = lines[0];
        else throw new FormatException();

        // Tek URL, tırnaklı URL ve DATABASE_URL="..." biçimlerini kabul et.
        for (var layer = 0; layer < 3; layer++)
        {
            value = Unquote(value);
            var assignment = MatchAssignment(value);
            if (!assignment.Success) return value;
            value = assignment.Groups[2].Value;
        }
        throw new FormatException();
    }

    private static Match MatchAssignment(string value) => Regex.Match(value,
        @"^(?:export\s+)?(DATABASE_URL|DATABASE_URL_POOLED|CONNECTION_STRING)\s*=\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string Unquote(string value)
    {
        value = value.Trim().Trim('\uFEFF').Trim();
        if (value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\''))
            value = value[1..^1].Trim();
        return value;
    }

    private static string FromPostgresUrl(string value)
    {
        // Hata mesajına URL/parola veya alttaki ayrıştırıcı hatası eklenmez.
        try
        {
            var uri = new Uri(value, UriKind.Absolute);
            var credentials = uri.UserInfo.Split(':', 2);
            if (string.IsNullOrWhiteSpace(uri.Host) || credentials.Length != 2 ||
                string.IsNullOrEmpty(credentials[0]) || uri.AbsolutePath.Length <= 1 || uri.Fragment.Length > 0)
                throw new FormatException();
            var password = Uri.UnescapeDataString(credentials[1]);
            if (password.Length > 0 && password.All(character => character == '*'))
                throw new FormatException();
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(credentials[0]), Password = password,
                Database = Uri.UnescapeDataString(uri.AbsolutePath[1..]), SslMode = SslMode.Require,
                CommandTimeout = 300
            };
            foreach (var option in QueryHelpers.ParseQuery(uri.Query))
            {
                if (option.Value.Count != 1) throw new FormatException();
                var setting = option.Value.ToString();
                switch (option.Key.ToLowerInvariant())
                {
                    case "sslmode":
                        builder.SslMode = setting.ToLowerInvariant() switch
                        {
                            "disable" => SslMode.Disable, "allow" => SslMode.Allow, "prefer" => SslMode.Prefer,
                            "require" => SslMode.Require, "verify-ca" => SslMode.VerifyCA, "verify-full" => SslMode.VerifyFull,
                            _ => throw new FormatException()
                        };
                        break;
                    case "channel_binding":
                        builder.ChannelBinding = setting.ToLowerInvariant() switch
                        {
                            "disable" => ChannelBinding.Disable, "prefer" => ChannelBinding.Prefer,
                            "require" => ChannelBinding.Require, _ => throw new FormatException()
                        };
                        break;
                    case "connect_timeout": builder.Timeout = int.Parse(setting, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "application_name": builder.ApplicationName = setting; break;
                    default: throw new FormatException();
                }
            }
            return builder.ConnectionString;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidOperationException("PostgreSQL bağlantı adresi geçersiz veya desteklenmeyen seçenek içeriyor. Neon DATABASE_URL değerini kontrol edin.");
        }
    }
}
