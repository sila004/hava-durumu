namespace Intern.Entities.DTOs.Weather;

public class WeatherDto
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Apparent { get; set; }
    public double Wind { get; set; }
    public int Code { get; set; }
}
