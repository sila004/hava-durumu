namespace Intern.Entities.Concretes;

public class WeatherMailAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string City { get; set; } = "İstanbul";
    public int DeliveryMinutes { get; set; } = 7 * 60;
    public bool DailyEnabled { get; set; }
    public string UnsubscribeToken { get; set; } = "";
    public DateOnly? LastSentDate { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? NextAttemptUtc { get; set; }
    public DateTimeOffset? LastTestSentUtc { get; set; }
}

public class WeatherLoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Redeemed { get; set; }
}
