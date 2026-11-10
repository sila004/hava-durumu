using Npgsql;
using Microsoft.AspNetCore.WebUtilities;

namespace Intern.API.Infrastructure;

/// <summary>Npgsql bağlantı dizesi, Neon DATABASE_URL adresi veya ayrı DB_* ayarlarını çözer.</summary>
public static class RuntimeConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var existing = new[] { configuration["CONNECTION_STRING"], configuration["DATABASE_URL"],
            configuration.GetConnectionString("DefaultConnection") }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (!string.IsNullOrWhiteSpace(existing))
            return existing.TrimStart().StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                existing.TrimStart().StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? FromPostgresUrl(existing.Trim()) : existing;

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
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host, Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
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
