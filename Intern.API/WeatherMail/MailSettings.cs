namespace Intern.API.WeatherMail;

public class MailSettings
{
    public bool Enabled { get; set; }
    public string DeliveryMode { get; set; } = "Smtp";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Meteoroloji";
    public string FrontendUrl { get; set; } = "http://localhost:3000";
    public string ApiUrl { get; set; } = "http://localhost:5100";
    public string PickupDirectory { get; set; } = "App_Data/mail-pickup";
}
