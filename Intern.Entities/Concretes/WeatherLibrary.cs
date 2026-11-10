namespace Intern.Entities.Concretes;

public class WeatherFavorite
{
    public Guid AccountId { get; set; }
    public string City { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}

public class WeatherPagePreference
{
    public Guid AccountId { get; set; }
    public string Section { get; set; } = "";
    public string City { get; set; } = "";
    public DateTimeOffset UpdatedUtc { get; set; }
}

// Karşılaştırma anındaki değerler saklanır; bu kayıt canlı hava verisi değildir.
public class WeatherComparison
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string FirstCity { get; set; } = "";
    public string SecondCity { get; set; } = "";
    public double FirstTemperature { get; set; }
    public double FirstHumidity { get; set; }
    public double FirstApparent { get; set; }
    public double FirstWind { get; set; }
    public int FirstCode { get; set; }
    public double SecondTemperature { get; set; }
    public double SecondHumidity { get; set; }
    public double SecondApparent { get; set; }
    public double SecondWind { get; set; }
    public int SecondCode { get; set; }
    public DateTimeOffset ComparedUtc { get; set; }
}
