using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Intern.API.WeatherMail;
using Intern.Data;
using Intern.Entities.Concretes;
using Intern.Service.Services.Abstracts;
using Intern.Service.Services.Concretes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intern.API.Controllers;

[ApiController]
[Route("api/weather-account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WeatherAccountController(AppDbContext db, WeatherMailSender sender, IOptions<MailSettings> mail,
    IConfiguration configuration, IWeatherService weather, ILogger<WeatherAccountController> logger) : ControllerBase
{
    public const string Scheme = "WeatherEmail";
    private readonly MailSettings settings = mail.Value;
    private string Secret => configuration["JWT:Secret"]!;

    [HttpGet("status")]
    public IActionResult Status() => Ok(new { mailReady = sender.IsReady, developmentPickup = sender.IsPickup,
        cities = WeatherService.CityNames, defaultDeliveryTime = "07:00" });

    [HttpGet("preview")]
    public ContentResult Preview()
    {
        Response.Headers.CacheControl = "no-store";
        return Content(WeatherEmailTemplate.Preview(settings), "text/html; charset=utf-8");
    }

    [HttpPost("request-code"), WeatherClient, EnableRateLimiting("email-code")]
    public async Task<IActionResult> RequestCode(EmailRequest request, CancellationToken ct)
    {
        if (!sender.IsReady) return StatusCode(503, new { message = "Mail gönderici henüz kurulmadı. Önizlemeyi inceleyebilirsin; giriş için sunucuda SMTP ayarlanmalı." });
        var email = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Aynı adrese paralel isteklerde de 60 saniyelik yeniden gönderim sınırı korunur.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({email}))", ct);
        if (await db.WeatherLoginChallenges.AnyAsync(x => x.Email == email && x.CreatedUtc > now.AddMinutes(-1), ct))
            return StatusCode(429, new { message = "Yeni kod istemeden önce 60 saniye bekle." });
        await db.WeatherLoginChallenges.Where(x => x.Email == email && !x.Redeemed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Redeemed, true), ct);
        await db.WeatherLoginChallenges.Where(x => x.ExpiresUtc < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        var code = EmailLoginCodes.Create();
        var challenge = new WeatherLoginChallenge { Email = email, CreatedUtc = now, ExpiresUtc = now.AddMinutes(10) };
        challenge.CodeHash = EmailLoginCodes.Hash(challenge.Id, code, Secret);
        db.WeatherLoginChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        try
        {
            await sender.SendAsync(email, "Meteoroloji giriş kodun", WeatherEmailTemplate.Login(code, settings),
                $"Meteoroloji giriş kodun: {code}. 10 dakika geçerli. Bu isteği sen yapmadıysan yok sayabilirsin.", ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Giriş maili gönderilemedi. Hata türü: {Type}", ex.GetType().Name);
            return StatusCode(502, new { message = "Giriş kodu gönderilemedi. Mail sunucusu ayarlarını kontrol edip tekrar dene." });
        }
        return Ok(new { challengeId = challenge.Id, expiresInSeconds = 600,
            message = sender.IsPickup ? "Geliştirme modu: kod mail olarak gönderilmedi, sunucunun yerel mail klasörüne yazıldı." : "Giriş kodu mail adresine gönderildi." });
    }

    [HttpPost("verify"), WeatherClient, EnableRateLimiting("email-verify")]
    public async Task<IActionResult> Verify(VerifyRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var attempts = await db.WeatherLoginChallenges.Where(x => x.Id == request.ChallengeId &&
            !x.Redeemed && x.ExpiresUtc > now && x.Attempts < 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
        if (attempts == 0) return BadRequest(new { message = "Kod geçersiz, süresi dolmuş veya deneme sınırı aşılmış. Yeni kod iste." });
        var challenge = await db.WeatherLoginChallenges.AsNoTracking().SingleAsync(x => x.Id == request.ChallengeId, ct);
        var hash = EmailLoginCodes.Hash(request.ChallengeId, request.Code, Secret);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(challenge.CodeHash)))
            return BadRequest(new { message = "Kod hatalı. En fazla 5 deneme yapabilirsin." });
        var redeemed = await db.WeatherLoginChallenges.Where(x => x.Id == challenge.Id && !x.Redeemed &&
                x.ExpiresUtc > now && x.Attempts <= 5)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Redeemed, true), ct);
        if (redeemed == 0) return BadRequest(new { message = "Kod daha önce kullanılmış. Yeni kod iste." });
        var id = Guid.NewGuid();
        var token = EmailLoginCodes.NewToken();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WeatherMailAccounts" ("Id", "Email", "City", "DeliveryMinutes", "DailyEnabled", "UnsubscribeToken")
            VALUES ({id}, {challenge.Email}, 'İstanbul', 420, FALSE, {token}) ON CONFLICT ("Email") DO NOTHING
            """, ct);
        var account = await db.WeatherMailAccounts.SingleAsync(x => x.Email == challenge.Email, ct);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, account.Id.ToString())], Scheme);
        await HttpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        { IsPersistent = true, ExpiresUtc = now.AddDays(30) });
        return Ok(AccountView(account));
    }

    [HttpGet("me"), Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var account = await FindAccount(ct);
        return account is null ? Unauthorized() : Ok(AccountView(account));
    }

    [HttpPut("subscription"), WeatherClient, Authorize(AuthenticationSchemes = Scheme)]
    public async Task<IActionResult> Subscription(SubscriptionRequest request, CancellationToken ct)
    {
        if (!WeatherService.CityNames.Contains(request.City)) return BadRequest(new { message = "Listeden bir şehir seç." });
        if (!TimeOnly.TryParseExact(request.DeliveryTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var delivery))
            return BadRequest(new { message = "Geçerli bir gönderim saati seç." });
        var account = await FindAccount(ct);
        if (account is null) return Unauthorized();
        account.City = request.City;
        account.DeliveryMinutes = delivery.Hour * 60 + delivery.Minute;
        account.DailyEnabled = request.DailyEnabled;
        if (request.DailyEnabled)
        {
            var local = DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow);
            var next = new DateTimeOffset(local.Year, local.Month, local.Day, delivery.Hour, delivery.Minute, 0, local.Offset);
            account.NextAttemptUtc = (next > local ? next : next.AddDays(1)).ToUniversalTime();
        }
        else account.NextAttemptUtc = null;
        await db.SaveChangesAsync(ct);
        return Ok(AccountView(account));
    }

    [HttpPost("test-mail"), WeatherClient, Authorize(AuthenticationSchemes = Scheme), EnableRateLimiting("email-test")]
    public async Task<IActionResult> TestMail(CancellationToken ct)
    {
        if (!sender.IsReady) return StatusCode(503, new { message = "Mail sunucusu henüz kurulmadı." });
        var account = await FindAccount(ct);
        if (account is null) return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var claimed = await db.WeatherMailAccounts.Where(x => x.Id == account.Id &&
            (x.LastTestSentUtc == null || x.LastTestSentUtc < now.AddMinutes(-1)))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastTestSentUtc, now), ct);
        if (claimed == 0) return StatusCode(429, new { message = "Yeni test maili için bir dakika bekle." });
        var forecast = await weather.GetForecastAsync(account.City, ct);
        if (!forecast.IsSuccess)
        {
            db.WeatherMailDeliveries.Add(new WeatherMailDelivery { AccountId = account.Id, City = account.City, Kind = "Test", ErrorCode = "WeatherUnavailable" });
            await db.SaveChangesAsync(ct);
            return StatusCode(502, new { message = "Hava tahmini alınamadı; test maili gönderilmedi." });
        }
        try { await SendDaily(account, forecast.Data, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Test maili gönderilemedi. Hata türü: {Type}", ex.GetType().Name);
            db.WeatherMailDeliveries.Add(new WeatherMailDelivery { AccountId = account.Id, City = account.City, Kind = "Test", ErrorCode = WeatherDeliveryErrors.From(ex) });
            await db.SaveChangesAsync(ct);
            return StatusCode(502, new { message = "Test maili gönderilemedi. Mail sunucusu ayarlarını kontrol et." });
        }
        db.WeatherMailDeliveries.Add(new WeatherMailDelivery { AccountId = account.Id, City = account.City, Kind = "Test", Succeeded = true });
        await db.SaveChangesAsync(ct);
        return Ok(new { message = sender.IsPickup ? "Test maili yerel geliştirme klasörüne yazıldı; gerçek mail gönderilmedi." : "Bugünün hava özeti mail adresine gönderildi." });
    }

    [HttpPost("logout"), WeatherClient]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Scheme);
        return Ok(new { message = "Çıkış yapıldı." });
    }

    [HttpGet("unsubscribe/{token}"), HttpPost("unsubscribe/{token}")]
    public async Task<IActionResult> Unsubscribe(string token, CancellationToken ct)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return NotFound();
        if (HttpMethods.IsPost(Request.Method))
        {
            await db.WeatherMailAccounts.Where(x => x.UnsubscribeToken == token)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DailyEnabled, false), ct);
            return Content("<html lang='tr'><meta charset='utf-8'><body style='background:#e9edf2;font-family:Arial;padding:40px;color:#243f70'><h1>Günlük mailler durduruldu.</h1><p>Hesap ayarlarından tekrar açabilirsin.</p></body></html>", "text/html; charset=utf-8");
        }
        return Content($"<html lang='tr'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><body style='background:#e9edf2;font-family:Arial;padding:40px;color:#243f70'><h1>Günlük hava maillerini durdur</h1><p>Bu tercihi hesap ayarlarından yeniden açabilirsin.</p><form method='post' action='/api/weather-account/unsubscribe/{token}'><button style='padding:14px;border:0;border-radius:10px;background:#294b7b;color:white'>Günlük mailleri durdur</button></form></body></html>", "text/html; charset=utf-8");
    }

    private async Task<WeatherMailAccount?> FindAccount(CancellationToken ct) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await db.WeatherMailAccounts.SingleOrDefaultAsync(x => x.Id == id, ct) : null;

    private object AccountView(WeatherMailAccount account) => new
    {
        account.Email, account.City, account.DailyEnabled,
        isAdmin = configuration.GetSection("WeatherAdmin").Get<WeatherAdminSettings>()?.Contains(account.Email) == true,
        deliveryTime = $"{account.DeliveryMinutes / 60:D2}:{account.DeliveryMinutes % 60:D2}",
        account.LastSentDate, nextDeliveryUtc = account.NextAttemptUtc,
        mailReady = sender.IsReady, developmentPickup = sender.IsPickup
    };

    private Task SendDaily(WeatherMailAccount account, Intern.Entities.DTOs.Weather.ForecastDto forecast, CancellationToken ct) =>
        sender.SendAsync(account.Email, $"{account.City} için bugünün hava özeti", WeatherEmailTemplate.Daily(account, forecast, settings),
            WeatherEmailTemplate.DailyText(account, forecast, settings), ct,
            $"{settings.ApiUrl.TrimEnd('/')}/api/weather-account/unsubscribe/{account.UnsubscribeToken}");
}

public sealed record EmailRequest([Required, EmailAddress, MaxLength(254)] string Email);
public sealed record VerifyRequest(Guid ChallengeId, [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record SubscriptionRequest([Required, MaxLength(80)] string City,
    [Required] string DeliveryTime, bool DailyEnabled);
