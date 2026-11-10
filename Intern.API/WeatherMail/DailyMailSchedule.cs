namespace Intern.API.WeatherMail;

public static class DailyMailSchedule
{
    public static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    public static DateTimeOffset LocalNow(DateTimeOffset utcNow) => TimeZoneInfo.ConvertTime(utcNow, Turkey);
    public static bool IsDue(bool enabled, int deliveryMinutes, DateOnly? lastSent,
        DateTimeOffset? leaseUntil, DateTimeOffset? nextAttempt, DateTimeOffset utcNow)
    {
        var local = LocalNow(utcNow);
        return enabled && local.Hour * 60 + local.Minute >= deliveryMinutes &&
            lastSent != DateOnly.FromDateTime(local.DateTime) &&
            (leaseUntil is null || leaseUntil <= utcNow) && (nextAttempt is null || nextAttempt <= utcNow);
    }
}
