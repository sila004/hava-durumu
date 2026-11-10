using System.Text.RegularExpressions;
using Intern.API.WeatherMail;
using Intern.Data;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Weather;
using Intern.Service.Services.Abstracts;
using Intern.Shared.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using Intern.API.Infrastructure;
using Npgsql;

// Yalnız açıkça istenen elle kurulum testi: alıcı da yapılandırılmış gönderici hesabıdır.
// Parola, adres veya ham SMTP yanıtı konsola yazılmaz.
if (args.Contains("--smtp-test"))
{
    DotNetEnv.Env.Load();
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var smtpSettings = configuration.GetSection("Mail").Get<MailSettings>() ?? new MailSettings();
    var smtpSender = new WeatherMailSender(Options.Create(smtpSettings), new TestEnvironment { EnvironmentName = "Production" });
    if (!smtpSender.IsReady || smtpSettings.Host != "smtp.gmail.com" || smtpSettings.Port != 587 ||
        !string.Equals(smtpSettings.Username, smtpSettings.FromAddress, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("SMTP_TEST_CONFIGURATION_INCOMPLETE");
        Environment.ExitCode = 1;
        return;
    }
    try
    {
        await smtpSender.SendAsync(smtpSettings.FromAddress, "Meteoroloji — mail bağlantın hazır",
            "<html lang='tr'><meta charset='utf-8'><body style='margin:0;padding:30px 12px;background:#e9edf2;font-family:Arial'><div style='max-width:540px;margin:auto;padding:30px;background:white;border-radius:20px'><p style='color:#243f70;letter-spacing:2px;font-weight:bold'>☀ METEOROLOJİ</p><h1 style='font-size:27px;color:#243f70'>Mail bağlantın hazır.</h1><p style='color:#617795;line-height:1.8'>Bu mail, Gmail SMTP bağlantısının başarıyla çalıştığını doğrulamak için gönderildi.</p><div style='background:#edf6f1;padding:18px;border-radius:12px;color:#376b58;line-height:1.8'>Sitedeki hesabına mail koduyla giriş yapıp şehrini seçerek her sabah 07:00 hava özetini açabilirsin.</div><p style='font-size:11px;color:#7b8b9e;margin-top:24px'>Bu bir bağlantı testidir; günlük hava tahmini değildir.</p></div></body></html>",
            "Meteoroloji mail bağlantı testi başarılı. Sitedeki hesabına giriş yapıp şehrini seçerek günlük 07:00 hava özetini açabilirsin. Bu bir bağlantı testidir.", default);
        Console.WriteLine("SMTP_TEST_ACCEPTED");
    }
    catch (SmtpException ex) { Console.WriteLine($"SMTP_TEST_FAILED status={ex.StatusCode}"); Environment.ExitCode = 1; }
    catch (Exception ex) { Console.WriteLine($"SMTP_TEST_FAILED type={ex.GetType().Name}"); Environment.ExitCode = 1; }
    return;
}

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
IConfiguration ConnectionConfig(params (string Key, string Value)[] settings) => new ConfigurationBuilder()
    .AddInMemoryCollection(settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))).Build();
var neonConnection = new NpgsqlConnectionStringBuilder(RuntimeConnectionString.Resolve(ConnectionConfig(
    ("DATABASE_URL", "postgresql://example_user:p%40ss%3Bword@ep-example.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"))));
Check(neonConnection.Host == "ep-example.eu-central-1.aws.neon.tech" && neonConnection.Port == 5432 && neonConnection.Database == "neondb", "Neon URL sunucu ve veritabanını çözer");
Check(neonConnection.Password == "p@ss;word" && neonConnection.SslMode == SslMode.Require && neonConnection.ChannelBinding == ChannelBinding.Require, "Neon parolası güvenle çözülür, SSL ve channel binding korunur");
Check(RuntimeConnectionString.Resolve(ConnectionConfig(("CONNECTION_STRING", "Host=localhost;Database=existing"), ("DATABASE_URL", "invalid"))) == "Host=localhost;Database=existing", "Mevcut CONNECTION_STRING önceliği korunur");
var localConnection = new NpgsqlConnectionStringBuilder(RuntimeConnectionString.Resolve(ConnectionConfig(("DB_HOST", "localhost"), ("DB_NAME", "local"), ("DB_USERNAME", "example"), ("DB_PASSWORD", "example"))));
Check(localConnection.Host == "localhost" && localConnection.Database == "local", "Yerel DB ayarları çalışmaya devam eder");
try
{
    RuntimeConnectionString.Resolve(ConnectionConfig(("DATABASE_URL", "postgresql://example:secret-marker@localhost/test?sslmode=invalid")));
    throw new Exception("Geçersiz URL reddedilmedi");
}
catch (InvalidOperationException error)
{
    Check(!error.ToString().Contains("secret-marker"), "Bağlantı hatası parolayı açığa çıkarmaz");
}
var morning = new DateTimeOffset(2026, 11, 4, 4, 0, 0, TimeSpan.Zero);
Check(!DailyMailSchedule.IsDue(true, 420, null, null, null, morning.AddSeconds(-1)), "07:00 öncesi gönderme");
Check(DailyMailSchedule.IsDue(true, 420, null, null, null, morning), "Türkiye saatiyle 07:00 gönderimi");
Check(!DailyMailSchedule.IsDue(true, 420, new DateOnly(2026, 11, 4), null, null, morning), "Aynı gün tekrar gönderme");
Check(DailyMailSchedule.IsDue(true, 420, new DateOnly(2026, 11, 4), null, null, morning.AddDays(1)), "Ertesi gün tekrar gönder");
Check(!DailyMailSchedule.IsDue(false, 420, null, null, null, morning), "Kapalı aboneliğe gönderme");
Check(!DailyMailSchedule.IsDue(true, 420, null, morning.AddMinutes(5), null, morning), "Başka işçinin aldığı kaydı gönderme");
Check(!DailyMailSchedule.IsDue(true, 420, null, null, morning.AddMinutes(5), morning), "Yeniden deneme saatine uy");
var challenge = Guid.NewGuid();
var code = EmailLoginCodes.Create();
Check(Regex.IsMatch(code, "^[0-9]{6}$"), "Altı haneli kod üret");
var hash = EmailLoginCodes.Hash(challenge, code, "test-secret-32-characters-long-value");
Check(hash.Length == 64 && hash != code, "Kod açık olarak saklanmaz");
Check(hash != EmailLoginCodes.Hash(Guid.NewGuid(), code, "test-secret-32-characters-long-value"), "Kod isteğe bağlıdır");
Check(EmailLoginCodes.NewToken().Length == 64, "Abonelik iptal bağlantısı rastgele token kullanır");
var mailSettings = new MailSettings();
Check(WeatherEmailTemplate.Preview(mailSettings).Contains("Canlı tahmin değildir"), "Önizleme örnek olduğunu belirtir");
Check(!WeatherEmailTemplate.Login("<script>", mailSettings).Contains("<script>"), "Mail dinamik metni HTML olarak kaçırır");
Check(!new WeatherMailSender(Options.Create(new MailSettings()), new TestEnvironment()).IsReady, "Gönderici kurulmadan mail hazır görünmez");
Check(!new WeatherMailSender(Options.Create(new MailSettings { Enabled = true, DeliveryMode = "Pickup" }),
    new TestEnvironment { EnvironmentName = "Production" }).IsReady, "Yayında geliştirme posta kutusu açılamaz");

var connection = Environment.GetEnvironmentVariable("MAIL_TEST_CONNECTION");
if (!string.IsNullOrEmpty(connection))
{
    var pickup = Environment.GetEnvironmentVariable("MAIL_TEST_PICKUP")!;
    var fakeWeather = new FakeWeather();
    var services = new ServiceCollection();
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connection));
    services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
    services.AddSingleton<IOptions<MailSettings>>(Options.Create(new MailSettings { Enabled = true, DeliveryMode = "Pickup", PickupDirectory = pickup }));
    services.AddScoped<WeatherMailSender>();
    services.AddSingleton<IWeatherService>(fakeWeather);
    await using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var account = new WeatherMailAccount { Email = "schedule@example.test", DailyEnabled = true, DeliveryMinutes = 0, UnsubscribeToken = EmailLoginCodes.NewToken() };
    db.WeatherMailAccounts.Add(account);
    await db.SaveChangesAsync();
    var job = new DailyWeatherMailBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<DailyWeatherMailBackgroundService>.Instance);
    var before = Directory.Exists(pickup) ? Directory.GetFiles(pickup, "*.eml").Length : 0;
    await job.RunOnceAsync(default);
    await job.RunOnceAsync(default);
    Check(Directory.GetFiles(pickup, "*.eml").Length == before + 1, "İşçi tekrar çalışsa da aynı gün tek mail");
    var saved = await db.WeatherMailAccounts.AsNoTracking().SingleAsync(x => x.Id == account.Id);
    Check(saved.LastSentDate == DateOnly.FromDateTime(DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow).DateTime), "Gönderim günü kalıcı kaydedilir");
    Check(await db.WeatherMailDeliveries.CountAsync(x => x.AccountId == account.Id && x.Succeeded && x.Kind == "Daily") == 1, "Başarılı günlük mail geçmişe yalnız bir kez yazılır");
    var retryAccount = new WeatherMailAccount { Email = "retry@example.test", DailyEnabled = true, DeliveryMinutes = 0, UnsubscribeToken = EmailLoginCodes.NewToken() };
    db.WeatherMailAccounts.Add(retryAccount);
    await db.SaveChangesAsync();
    fakeWeather.Unavailable = true;
    await job.RunOnceAsync(default);
    var failed = await db.WeatherMailAccounts.AsNoTracking().SingleAsync(x => x.Id == retryAccount.Id);
    Check(failed.LastSentDate is null && failed.NextAttemptUtc > DateTimeOffset.UtcNow, "Sağlayıcı hatası gönderildi sayılmaz, yeniden denenir");
    Check(await db.WeatherMailDeliveries.AnyAsync(x => x.AccountId == retryAccount.Id && !x.Succeeded && x.ErrorCode == "WeatherUnavailable"), "Sağlayıcı hatası güvenli kategoriyle geçmişe yazılır");
    fakeWeather.Unavailable = false;
    await db.WeatherMailAccounts.Where(x => x.Id == retryAccount.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
    await job.RunOnceAsync(default);
    Check(Directory.GetFiles(pickup, "*.eml").Length == before + 2, "Sağlayıcı düzeldiğinde gönderim tamamlanır");
}
Console.WriteLine($"{checks} kontrol geçti.");

sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Checks";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

sealed class FakeWeather : IWeatherService
{
    public bool Unavailable { get; set; }
    public Task<Response<ForecastDto>> GetForecastAsync(string? city, CancellationToken ct = default, double? latitude = null, double? longitude = null)
    {
        if (Unavailable) return Task.FromResult(Response<ForecastDto>.Fail("Unavailable", "test", 502, true));
        var day = DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
        return Task.FromResult(Response<ForecastDto>.Success(new ForecastDto
        {
            Current = new CurrentWeatherDto { Time = day + "T07:00", Temperature = 15, Apparent = 14, Humidity = 65, Wind = 8, WindDirection = 40, Pressure = 1013, Precipitation = 0, CloudCover = 20, Code = 2, IsDay = 1 },
            Daily = new DailyWeatherDto { Time = [day], Code = [2], MaximumTemperature = [18], MinimumTemperature = [11], PrecipitationProbability = [35], Sunrise = [day + "T07:30"], Sunset = [day + "T17:30"] },
            Hourly = new HourlyWeatherDto { Time = [day + "T09:00"], Temperature = [16], Apparent = [15], Code = [2], PrecipitationProbability = [10], Precipitation = [0], Wind = [8], Humidity = [60], IsDay = [1], UvIndex = [2] }
        }, 200));
    }
    public Task<Response<WeatherDto>> GetWeatherAsync(string? city, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Response<AirQualityDto>> GetAirQualityAsync(string? city, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Response<AirQualityDetailDto>> GetAirQualityDetailAsync(string? city, CancellationToken ct = default, double? latitude = null, double? longitude = null) => throw new NotSupportedException();
}
