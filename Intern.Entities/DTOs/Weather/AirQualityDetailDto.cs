using System.Text.Json.Serialization;

namespace Intern.Entities.DTOs.Weather;

public class AirQualityDetailDto
{
    public required AirQualityCurrentDto Current { get; set; }
    [JsonPropertyName("current_units")]
    public required Dictionary<string, string> CurrentUnits { get; set; }
}

public class AirQualityCurrentDto
{
    public required string Time { get; set; }
    [JsonPropertyName("european_aqi")] public double? Aqi { get; set; }
    [JsonPropertyName("pm2_5")] public double? Pm25 { get; set; }
    [JsonPropertyName("pm10")] public double? Pm10 { get; set; }
    public double? Ozone { get; set; }
    [JsonPropertyName("nitrogen_dioxide")] public double? NitrogenDioxide { get; set; }
    [JsonPropertyName("carbon_monoxide")] public double? CarbonMonoxide { get; set; }
    [JsonPropertyName("sulphur_dioxide")] public double? SulphurDioxide { get; set; }
}
