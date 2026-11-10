namespace Intern.Shared.Settings;

public class WeatherSettings
{
    public const string SectionName = "Weather";
    public string ForecastUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";
    public string AirQualityUrl { get; set; } = "https://air-quality-api.open-meteo.com/v1/air-quality";
    public int TimeoutSeconds { get; set; } = 15;
    public int CacheSeconds { get; set; } = 600;
    public int RateLimitBackoffSeconds { get; set; } = 300;
}
