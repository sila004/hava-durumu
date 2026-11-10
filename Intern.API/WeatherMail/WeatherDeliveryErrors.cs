using System.Net.Mail;

namespace Intern.API.WeatherMail;

public static class WeatherDeliveryErrors
{
    public static string From(Exception error) => error switch
    {
        SmtpException => "SmtpFailure",
        OperationCanceledException => "Timeout",
        HttpRequestException => "WeatherUnavailable",
        InvalidOperationException => "WeatherUnavailable",
        _ => "DeliveryFailure"
    };
}
