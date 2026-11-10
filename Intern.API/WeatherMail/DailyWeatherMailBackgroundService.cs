using Intern.Data;
using Intern.Entities.Concretes;
using Intern.Service.Services.Abstracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intern.API.WeatherMail;

public class DailyWeatherMailBackgroundService(IServiceScopeFactory scopes,
    ILogger<DailyWeatherMailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Günlük mail kontrolü tamamlanamadı. Hata türü: {Type}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<WeatherMailSender>();
        if (!sender.IsReady) return;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<MailSettings>>().Value;
        var weather = scope.ServiceProvider.GetRequiredService<IWeatherService>();
        var now = DateTimeOffset.UtcNow;
        var local = DailyMailSchedule.LocalNow(now);
        var today = DateOnly.FromDateTime(local.DateTime);
        var minutes = local.Hour * 60 + local.Minute;
        var accounts = await db.WeatherMailAccounts.AsNoTracking().Where(x => x.DailyEnabled &&
            x.DeliveryMinutes <= minutes && (x.LastSentDate == null || x.LastSentDate != today) &&
            (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now) &&
            (x.NextAttemptUtc == null || x.NextAttemptUtc <= now)).OrderBy(x => x.Id).Take(100).ToListAsync(ct);

        foreach (var account in accounts)
        {
            var claimNow = DateTimeOffset.UtcNow;
            if (DateOnly.FromDateTime(DailyMailSchedule.LocalNow(claimNow).DateTime) != today) break;
            var claimed = await db.WeatherMailAccounts.Where(x => x.Id == account.Id && x.DailyEnabled &&
                x.City == account.City && x.DeliveryMinutes == account.DeliveryMinutes &&
                (x.LastSentDate == null || x.LastSentDate != today) &&
                (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= claimNow) &&
                (x.NextAttemptUtc == null || x.NextAttemptUtc <= claimNow))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntilUtc, claimNow.AddMinutes(10)), ct);
            if (claimed == 0) continue;
            string? failure = null;
            try
            {
                var forecast = await weather.GetForecastAsync(account.City, ct);
                if (!forecast.IsSuccess) throw new InvalidOperationException("Hava sağlayıcısı tahmin döndürmedi.");
                var unsubscribe = $"{settings.ApiUrl.TrimEnd('/')}/api/weather-account/unsubscribe/{account.UnsubscribeToken}";
                await sender.SendAsync(account.Email, $"{account.City} için bugünün hava özeti",
                    WeatherEmailTemplate.Daily(account, forecast.Data, settings),
                    WeatherEmailTemplate.DailyText(account, forecast.Data, settings), ct, unsubscribe);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Günlük hava maili gönderilemedi. Hesap: {Id}, hata türü: {Type}", account.Id, ex.GetType().Name);
                failure = WeatherDeliveryErrors.From(ex);
            }
            // Sonuç ve zamanlayıcı durumu birlikte kaydedilir. Veritabanı hatası
            // gönderim hatası gibi etiketlenmez; üst döngüye iletilir.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (failure is null)
                await db.WeatherMailAccounts.Where(x => x.Id == account.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.LastSentDate, today).SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.NextAttemptUtc, (DateTimeOffset?)null), ct);
            else
                await db.WeatherMailAccounts.Where(x => x.Id == account.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.NextAttemptUtc, DateTimeOffset.UtcNow.AddMinutes(15)), ct);
            db.WeatherMailDeliveries.Add(new WeatherMailDelivery
            { AccountId = account.Id, City = account.City, Succeeded = failure is null, ErrorCode = failure });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
