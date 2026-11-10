using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Intern.API.WeatherMail;
using Intern.Data;
using Intern.Entities.Concretes;
using Intern.Service.Services.Concretes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Intern.API.Controllers;

[ApiController]
[Route("api/weather-admin")]
[Authorize(Policy = WeatherAdminRequirement.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class WeatherAdminController(AppDbContext db, WeatherMailSender sender, ILogger<WeatherAdminController> logger) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var local = DailyMailSchedule.LocalNow(now);
        var midnight = new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset).ToUniversalTime();
        var daily = db.WeatherMailDeliveries.Where(x => x.AttemptedUtc >= midnight && x.Kind == "Daily");
        return Ok(new
        {
            totalAccounts = await db.WeatherMailAccounts.CountAsync(ct),
            activeSubscriptions = await db.WeatherMailAccounts.CountAsync(x => x.DailyEnabled, ct),
            sentToday = await daily.CountAsync(x => x.Succeeded, ct),
            failedToday = await daily.CountAsync(x => !x.Succeeded, ct),
            mailReady = sender.IsReady, developmentPickup = sender.IsPickup,
            cities = WeatherService.CityNames, updatedUtc = now
        });
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> Accounts([FromQuery, MaxLength(254)] string? search,
        [FromQuery] bool? enabled, [FromQuery, Range(1, 1000000)] int page = 1, CancellationToken ct = default)
    {
        var query = db.WeatherMailAccounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email.ToLower().Contains(term) || x.City.ToLower().Contains(term));
        }
        if (enabled.HasValue) query = query.Where(x => x.DailyEnabled == enabled.Value);
        var total = await query.CountAsync(ct);
        var records = await query.OrderBy(x => x.Email).Skip((page - 1) * 20).Take(20)
            .Select(x => new { x.Id, x.Email, x.City, x.DeliveryMinutes, x.DailyEnabled, x.LastSentDate,
                x.NextAttemptUtc, x.LeaseUntilUtc,
                lastDelivery = db.WeatherMailDeliveries.Where(d => d.AccountId == x.Id && d.Kind == "Daily")
                    .OrderByDescending(d => d.AttemptedUtc).Select(d => new { d.Succeeded, d.ErrorCode, d.AttemptedUtc }).FirstOrDefault() })
            .ToListAsync(ct);
        return Ok(new { items = records.Select(x => new { x.Id, x.Email, x.City, x.DailyEnabled, x.LastSentDate,
            deliveryTime = $"{x.DeliveryMinutes / 60:D2}:{x.DeliveryMinutes % 60:D2}", x.NextAttemptUtc,
            processing = x.LeaseUntilUtc > DateTimeOffset.UtcNow, x.lastDelivery }), total, page, pageSize = 20 });
    }

    [HttpPut("accounts/{id:guid}/subscription"), WeatherClient]
    public async Task<IActionResult> Subscription(Guid id, SubscriptionRequest request, CancellationToken ct)
    {
        if (!WeatherService.CityNames.Contains(request.City) ||
            !TimeOnly.TryParseExact(request.DeliveryTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var delivery))
            return BadRequest(new { message = "Geçerli bir şehir ve gönderim saati seç." });
        var now = DateTimeOffset.UtcNow;
        var local = DailyMailSchedule.LocalNow(now);
        var next = new DateTimeOffset(local.Year, local.Month, local.Day, delivery.Hour, delivery.Minute, 0, local.Offset);
        DateTimeOffset? nextAttempt = request.DailyEnabled ? (next > local ? next : next.AddDays(1)).ToUniversalTime() : null;
        var changed = await db.WeatherMailAccounts.Where(x => x.Id == id && (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.City, request.City)
                .SetProperty(x => x.DeliveryMinutes, delivery.Hour * 60 + delivery.Minute)
                .SetProperty(x => x.DailyEnabled, request.DailyEnabled).SetProperty(x => x.NextAttemptUtc, nextAttempt), ct);
        if (changed == 0) return await MissingOrBusy(id, ct);
        logger.LogInformation("Yönetici abonelik ayarını değiştirdi. Yönetici: {Admin}, hesap: {AccountId}", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, id);
        return Ok(new { message = "Abonelik tercihleri kaydedildi." });
    }

    [HttpPost("accounts/{id:guid}/retry"), WeatherClient]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DailyMailSchedule.LocalNow(now).DateTime);
        // Yalnız başarısız günlük gönderim yeniden sıraya alınabilir. İptal tercihi ve günlük tek gönderim korunur.
        var changed = await db.WeatherMailAccounts.Where(x => x.Id == id && x.DailyEnabled &&
                (x.LastSentDate == null || x.LastSentDate != today) &&
                (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now) &&
                db.WeatherMailDeliveries.Where(d => d.AccountId == id && d.Kind == "Daily")
                    .OrderByDescending(d => d.AttemptedUtc).Select(d => (bool?)d.Succeeded).FirstOrDefault() == false)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptUtc, now), ct);
        if (changed == 0)
        {
            if (!await db.WeatherMailAccounts.AnyAsync(x => x.Id == id, ct)) return NotFound();
            return Conflict(new { message = "Yeniden deneme için aktif abonelik ve başarısız günlük gönderim gerekir. Bugün gönderilmiş veya işlenmekte olan mail tekrar gönderilmez." });
        }
        logger.LogInformation("Yönetici yeniden gönderimi sıraya aldı. Yönetici: {Admin}, hesap: {AccountId}", User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, id);
        return Ok(new { message = "Gönderim yeniden sıraya alındı. Zamanlanmış görev uygun kayıtları dakikada bir kontrol eder." });
    }

    [HttpGet("deliveries")]
    public async Task<IActionResult> Deliveries([FromQuery] bool? succeeded,
        [FromQuery, Range(1, 1000000)] int page = 1, CancellationToken ct = default)
    {
        var query = db.WeatherMailDeliveries.AsNoTracking();
        if (succeeded.HasValue) query = query.Where(x => x.Succeeded == succeeded.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.AttemptedUtc).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Join(db.WeatherMailAccounts, delivery => delivery.AccountId, account => account.Id,
                (delivery, account) => new { delivery.Id, delivery.AccountId, account.Email, delivery.City,
                    delivery.Kind, delivery.AttemptedUtc, delivery.Succeeded, delivery.ErrorCode }).ToListAsync(ct);
        return Ok(new { items, total, page, pageSize = 20 });
    }

    private async Task<IActionResult> MissingOrBusy(Guid id, CancellationToken ct) =>
        await db.WeatherMailAccounts.AnyAsync(x => x.Id == id, ct)
            ? Conflict(new { message = "Bu hesap için gönderim sürüyor. Biraz sonra tekrar dene." }) : NotFound();
}
