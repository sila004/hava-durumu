using System.Text.Json.Serialization;

namespace Intern.Entities.DTOs.Weather;

// Ana sayfanın kullandığı alan adlarını korur; dış API'nin diğer alanları dışarı aktarılmaz.
public class ForecastDto
{
    public string Timezone { get; set; } = "Europe/Istanbul";
    public required CurrentWeatherDto Current { get; set; }
    public required DailyWeatherDto Daily { get; set; }
    public required HourlyWeatherDto Hourly { get; set; }
}

public class CurrentWeatherDto
{
    public required string Time { get; set; }
    [JsonPropertyName("temperature_2m")] public required double Temperature { get; set; }
    [JsonPropertyName("apparent_temperature")] public required double Apparent { get; set; }
    [JsonPropertyName("relative_humidity_2m")] public required double Humidity { get; set; }
    [JsonPropertyName("wind_speed_10m")] public required double Wind { get; set; }
    [JsonPropertyName("wind_direction_10m")] public required double WindDirection { get; set; }
    [JsonPropertyName("surface_pressure")] public required double Pressure { get; set; }
    public required double Precipitation { get; set; }
    [JsonPropertyName("cloud_cover")] public required double CloudCover { get; set; }
    [JsonPropertyName("weather_code")] public required int Code { get; set; }
    [JsonPropertyName("is_day")] public required int IsDay { get; set; }
}

public class DailyWeatherDto
{
    public required string[] Time { get; set; }
    [JsonPropertyName("weather_code")] public required int[] Code { get; set; }
    [JsonPropertyName("temperature_2m_max")] public required double[] MaximumTemperature { get; set; }
    [JsonPropertyName("temperature_2m_min")] public required double[] MinimumTemperature { get; set; }
    [JsonPropertyName("precipitation_probability_max")] public required double[] PrecipitationProbability { get; set; }
    public required string[] Sunrise { get; set; }
    public required string[] Sunset { get; set; }
}

public class HourlyWeatherDto
{
    public required string[] Time { get; set; }
    [JsonPropertyName("temperature_2m")] public required double[] Temperature { get; set; }
    [JsonPropertyName("apparent_temperature")] public required double[] Apparent { get; set; }
    [JsonPropertyName("weather_code")] public required int[] Code { get; set; }
    [JsonPropertyName("precipitation_probability")] public required double[] PrecipitationProbability { get; set; }
    public required double[] Precipitation { get; set; }
    [JsonPropertyName("wind_speed_10m")] public required double[] Wind { get; set; }
    [JsonPropertyName("relative_humidity_2m")] public required double[] Humidity { get; set; }
    [JsonPropertyName("is_day")] public required int[] IsDay { get; set; }
    [JsonPropertyName("uv_index")] public required double[] UvIndex { get; set; }
}
