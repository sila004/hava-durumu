using System.Globalization;
using System.Text.Json;
using Intern.Entities.DTOs.Weather;

namespace Intern.Service.Services.Concretes;

// Farklı sağlayıcının yanıtını uygulamanın mevcut tahmin sözleşmesine dönüştürür.
public static class VisualCrossingForecast
{
    public static ForecastDto Parse(JsonElement root)
    {
        var days = root.GetProperty("days").EnumerateArray().Take(7).ToArray();
        if (days.Length != 7) throw new JsonException("Yedi günlük tahmin eksik.");
        var current = root.GetProperty("currentConditions");
        var offset = current.TryGetProperty("tzoffset", out var localOffset) ? localOffset.GetDouble() : Number(root, "tzoffset");
        var currentTime = DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("datetimeEpoch").GetInt64())
            .ToOffset(TimeSpan.FromHours(offset)).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        var today = days.FirstOrDefault(day => Text(day, "datetime") == currentTime[..10]);
        if (today.ValueKind == JsonValueKind.Undefined) throw new JsonException("Güncel hava tarihi tahminle uyuşmuyor.");
        // Anonim erişimde sonraki günlerin saatlik bölümü bulunmayabilir; günlük tahmin korunur.
        var hours = days.SelectMany(day =>
            (day.TryGetProperty("hours", out var section) && section.ValueKind == JsonValueKind.Array
                ? section.EnumerateArray().ToArray() : Array.Empty<JsonElement>())
            .Select(hour => (Day: day, Hour: hour))).ToArray();
        return new ForecastDto
        {
            Timezone = Text(root, "timezone"),
            Current = new CurrentWeatherDto
            {
                Time = currentTime, Temperature = Number(current, "temp"), Apparent = Number(current, "feelslike"),
                Humidity = Number(current, "humidity"), Wind = Number(current, "windspeed"),
                WindDirection = Number(current, "winddir"), Pressure = null,
                Precipitation = OptionalNumber(current, "precip"), CloudCover = Number(current, "cloudcover"),
                Code = Code(current), IsDay = IsDay(today, currentTime[11..])
            },
            Daily = new DailyWeatherDto
            {
                Time = days.Select(day => Text(day, "datetime")).ToArray(),
                Code = days.Select(Code).ToArray(),
                MaximumTemperature = days.Select(day => Number(day, "tempmax")).ToArray(),
                MinimumTemperature = days.Select(day => Number(day, "tempmin")).ToArray(),
                PrecipitationProbability = days.Select(day => Number(day, "precipprob")).ToArray(),
                Sunrise = days.Select(day => LocalDateTime(day, Text(day, "sunrise"))).ToArray(),
                Sunset = days.Select(day => LocalDateTime(day, Text(day, "sunset"))).ToArray()
            },
            Hourly = new HourlyWeatherDto
            {
                Time = hours.Select(row => LocalDateTime(row.Day, Text(row.Hour, "datetime"))).ToArray(),
                Temperature = hours.Select(row => Number(row.Hour, "temp")).ToArray(),
                Apparent = hours.Select(row => Number(row.Hour, "feelslike")).ToArray(),
                Code = hours.Select(row => Code(row.Hour)).ToArray(),
                PrecipitationProbability = hours.Select(row => Number(row.Hour, "precipprob")).ToArray(),
                Precipitation = hours.Select(row => Number(row.Hour, "precip")).ToArray(),
                Wind = hours.Select(row => Number(row.Hour, "windspeed")).ToArray(),
                Humidity = hours.Select(row => Number(row.Hour, "humidity")).ToArray(),
                IsDay = hours.Select(row => IsDay(row.Day, Text(row.Hour, "datetime"))).ToArray(),
                UvIndex = hours.Select(row => OptionalNumber(row.Hour, "uvindex")).ToArray()
            }
        };
    }

    private static string Text(JsonElement row, string key) => row.GetProperty(key).GetString()
        ?? throw new JsonException($"{key} alanı eksik.");
    private static double Number(JsonElement row, string key) => OptionalNumber(row, key)
        ?? throw new JsonException($"{key} alanı eksik.");
    private static double? OptionalNumber(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var result) && double.IsFinite(result) ? result : null;
    private static string LocalDateTime(JsonElement day, string time) => Text(day, "datetime") + "T" + time[..5];
    private static int IsDay(JsonElement day, string time) =>
        TimeOnly.Parse(time, CultureInfo.InvariantCulture) >= TimeOnly.Parse(Text(day, "sunrise"), CultureInfo.InvariantCulture) &&
        TimeOnly.Parse(time, CultureInfo.InvariantCulture) < TimeOnly.Parse(Text(day, "sunset"), CultureInfo.InvariantCulture) ? 1 : 0;
    private static int Code(JsonElement row)
    {
        var conditions = Text(row, "conditions").ToLowerInvariant();
        if (conditions.Contains("thunder")) return 95;
        if (conditions.Contains("freezing rain") || conditions.Contains("ice")) return 67;
        return Text(row, "icon") switch
        {
            "clear-day" or "clear-night" => 0,
            "partly-cloudy-day" or "partly-cloudy-night" => 2,
            "cloudy" or "wind" => 3,
            "fog" => 45,
            "rain" => 63,
            "snow" => 73,
            _ => throw new JsonException("Hava koşulu kodu tanınmıyor.")
        };
    }
}
