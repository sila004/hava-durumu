using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace Intern.API.WeatherMail;

public class WeatherMailSender(IOptions<MailSettings> options, IWebHostEnvironment environment)
{
    private readonly MailSettings settings = options.Value;
    public bool IsPickup => environment.IsDevelopment() && settings.DeliveryMode == "Pickup";
    public bool IsReady => settings.Enabled && (IsPickup ||
        (settings.DeliveryMode == "Smtp" && !string.IsNullOrWhiteSpace(settings.Host) &&
         MailAddress.TryCreate(settings.FromAddress, out _) &&
         !string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password)));

    public async Task SendAsync(string recipient, string subject, string html, string text,
        CancellationToken ct, string? unsubscribeUrl = null)
    {
        if (!IsReady) throw new InvalidOperationException("Mail gönderici henüz yapılandırılmadı.");
        using var message = new MailMessage
        {
            From = new MailAddress(IsPickup ? "preview@example.test" : settings.FromAddress, settings.FromName),
            Subject = subject, SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8,
            Body = text, IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));
        if (unsubscribeUrl is not null)
        {
            message.Headers.Add("List-Unsubscribe", $"<{unsubscribeUrl}>");
            message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }
        using var smtp = new SmtpClient();
        if (IsPickup)
        {
            var directory = Path.GetFullPath(settings.PickupDirectory, environment.ContentRootPath);
            Directory.CreateDirectory(directory);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = directory;
        }
        else
        {
            smtp.Host = settings.Host;
            smtp.Port = settings.Port;
            smtp.EnableSsl = true; // STARTTLS (genellikle 587); parola açık bağlantıdan gönderilmez.
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
