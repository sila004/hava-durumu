namespace Intern.Entities.Concretes;

public class WeatherMailDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string City { get; set; } = "";
    public string Kind { get; set; } = "Daily";
    public DateTimeOffset AttemptedUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Succeeded { get; set; }
    // Ham SMTP yanıtı, adres veya kimlik bilgileri kaydedilmez.
    public string? ErrorCode { get; set; }
}
