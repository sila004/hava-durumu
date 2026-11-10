using System.Globalization;
using System.Net;
using Intern.Entities.Concretes;
using Intern.Entities.DTOs.Weather;

namespace Intern.API.WeatherMail;

public static class WeatherEmailTemplate
{
    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Login(string code, MailSettings settings) => Frame("Giriş kodun hazır",
        $"""
        <p style="color:#617795;font-size:11px;letter-spacing:2px">HESABINA HOŞ GELDİN</p>
        <h1 style="font-size:28px;color:#243f70;margin:12px 0">Bir adım sonra içeridesin.</h1>
        <p style="line-height:1.8;color:#617795">Meteoroloji hesabına giriş yapmak için aşağıdaki tek kullanımlık kodu gir.</p>
        <div style="padding:24px;background:#edf3fb;border-radius:14px;text-align:center;font-size:36px;letter-spacing:10px;color:#243f70;font-weight:bold">{E(code)}</div>
        <p style="line-height:1.8;color:#617795;font-size:13px">Kod 10 dakika geçerli. Bu isteği sen yapmadıysan maili yok sayabilirsin. Kodunu kimseyle paylaşma.</p>
        """, settings);

    public static string Daily(WeatherMailAccount account, ForecastDto forecast, MailSettings settings)
    {
        var today = DateOnly.FromDateTime(DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow).DateTime);
        var index = Array.IndexOf(forecast.Daily.Time, today.ToString("yyyy-MM-dd"));
        if (index < 0) throw new InvalidOperationException("Bugünün tahmin verisi bulunamadı.");
        return RenderDaily(account.City, today, forecast.Daily.MaximumTemperature[index],
            forecast.Daily.MinimumTemperature[index], forecast.Daily.PrecipitationProbability[index],
            forecast.Daily.Code[index], forecast.Daily.Sunrise[index], forecast.Daily.Sunset[index],
            settings, $"{settings.ApiUrl.TrimEnd('/')}/api/weather-account/unsubscribe/{account.UnsubscribeToken}", false);
    }

    public static string Preview(MailSettings settings) => RenderDaily("İstanbul",
        DateOnly.FromDateTime(DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow).DateTime),
        18, 11, 35, 2, "2026-11-04T07:36", "2026-11-04T17:57", settings, null, true);

    public static string DailyText(WeatherMailAccount account, ForecastDto forecast, MailSettings settings)
    {
        var day = DateOnly.FromDateTime(DailyMailSchedule.LocalNow(DateTimeOffset.UtcNow).DateTime);
        var index = Array.IndexOf(forecast.Daily.Time, day.ToString("yyyy-MM-dd"));
        if (index < 0) throw new InvalidOperationException("Bugünün tahmin verisi bulunamadı.");
        return $"""
            Günaydın, {account.City}. {day.ToString("d MMMM yyyy", Turkish)} hava özetin:
            {Condition(forecast.Daily.Code[index])}
            En yüksek: {Math.Round(forecast.Daily.MaximumTemperature[index])}°C
            En düşük: {Math.Round(forecast.Daily.MinimumTemperature[index])}°C
            Yağış olasılığı: %{Math.Round(forecast.Daily.PrecipitationProbability[index])}
            Saatlik tahminler: {settings.FrontendUrl}
            Kaynak: Open-Meteo. Tahminler değişebilir.
            Günlük mailleri durdur: {settings.ApiUrl.TrimEnd('/')}/api/weather-account/unsubscribe/{account.UnsubscribeToken}
            """;
    }

    public static string Condition(int code) => code switch
    {
        0 => "Açık", 1 or 2 => "Parçalı bulutlu", 3 => "Bulutlu", 45 or 48 => "Sisli",
        >= 51 and <= 67 or >= 80 and <= 82 => "Yağmurlu",
        >= 71 and <= 77 or 85 or 86 => "Kar yağışlı", >= 95 => "Gök gürültülü", _ => "Değişken"
    };

    private static string RenderDaily(string city, DateOnly day, double max, double min, double rain,
        int code, string sunrise, string sunset, MailSettings settings, string? unsubscribeUrl, bool preview)
    {
        var note = rain >= 50 ? "Bugün yağış ihtimali yüksek. Dışarı çıkarken şemsiyeni yanına al." :
            max <= 12 ? "Serin bir gün seni bekliyor. Dışarı çıkarken üzerine bir kat daha alabilirsin." :
            max >= 30 ? "Bugün hava sıcak. Açık hava planlarını daha serin saatlere bırakabilirsin." :
            "Gününü planlamadan önce saatlik tahminlere göz at. Hava gün içinde değişebilir.";
        var footer = unsubscribeUrl is null ?
            "Bu bir tasarım önizlemesidir. Gösterilen değerler örnektir; canlı tahmin değildir." :
            $"Bu özeti hesabındaki tercihin doğrultusunda alıyorsun. <a href=\"{E(unsubscribeUrl)}\" style=\"color:#617795\">Günlük mailleri durdur</a> · <a href=\"{E(settings.FrontendUrl.TrimEnd('/') + "/hesabim")}\" style=\"color:#617795\">Mail tercihleri</a>";
        return Frame($"{city} için bugünün hava özeti", $"""
        {(preview ? "<p style=\"padding:10px;background:#fff3d8;color:#76561b;font-size:12px;border-radius:8px\">ÖRNEK GÖRÜNÜM · Canlı tahmin değildir</p>" : "")}
        <p style="color:#617795;font-size:11px;letter-spacing:2px">GÜNE HAZIR BAŞLA</p>
        <h1 style="font-size:28px;color:#243f70;margin:10px 0 8px">Günaydın, {E(city)}.</h1>
        <p style="color:#617795;font-size:13px;margin:0 0 24px">{E(day.ToString("d MMMM yyyy, dddd", Turkish))} · Günlük hava özetin</p>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#294b7b;border-radius:18px;color:#ffffff">
          <tr><td style="padding:28px 28px 12px;color:#d4e2f6;font-size:11px;letter-spacing:2px">BUGÜN SENİ NE BEKLİYOR?</td></tr>
          <tr><td style="padding:0 28px;font-size:56px;font-weight:bold;line-height:1.2">{Math.Round(max)}°<span style="font-size:18px;font-weight:normal">C</span></td></tr>
          <tr><td style="padding:8px 28px 28px;font-size:19px">{E(Condition(code))}<p style="font-size:12px;color:#d4e2f6;margin:12px 0 0">Günün en yüksek tahmini sıcaklığı</p></td></tr>
        </table>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:18px 0">
          <tr>
            <td width="50%" style="padding:18px;background:#edf3fa;border-radius:12px;border-right:6px solid #ffffff"><span style="font-size:12px;color:#617795">En düşük sıcaklık</span><p style="margin:9px 0 0;font-size:25px;color:#243f70;font-weight:bold">{Math.Round(min)}°C</p></td>
            <td width="50%" style="padding:18px;background:#edf3fa;border-radius:12px;border-left:6px solid #ffffff"><span style="font-size:12px;color:#617795">Yağış olasılığı</span><p style="margin:9px 0 0;font-size:25px;color:#243f70;font-weight:bold">%{Math.Round(rain)}</p></td>
          </tr>
        </table>
        <p style="color:#617795;font-size:12px;text-align:center">Gün doğumu <strong>{E(sunrise.Length >= 16 ? sunrise[11..16] : "—")}</strong> &nbsp; · &nbsp; Gün batımı <strong>{E(sunset.Length >= 16 ? sunset[11..16] : "—")}</strong></p>
        <div style="padding:18px 20px;border:1px solid #d5e3df;border-radius:12px;background:#f1f8f4;margin:22px 0"><strong style="font-size:13px;color:#376b58">Günün notu</strong><p style="margin:8px 0 0;color:#54716b;line-height:1.8;font-size:13px">{E(note)}</p></div>
        <p style="text-align:center;margin:28px 0"><a href="{E(settings.FrontendUrl)}" style="display:inline-block;padding:15px 25px;background:#294b7b;color:#ffffff;border-radius:10px;text-decoration:none;font-size:13px;font-weight:bold">Saatlik tahminleri incele ↗</a></p>
        <p style="font-size:11px;color:#78899e;line-height:1.8;text-align:center">Veri kaynakları: <a href="https://open-meteo.com/">Open-Meteo</a> · <a href="https://www.visualcrossing.com/">Visual Crossing Weather</a>. Tahminler değişebilir.<br>{footer}</p>
        """, settings);
    }

    private static string Frame(string title, string content, MailSettings settings) => $"""
        <!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(title)}</title></head>
        <body style="margin:0;padding:0;background:#e9edf2;font-family:Arial,Helvetica,sans-serif">
        <div style="display:none;max-height:0;overflow:hidden">{E(title)}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#e9edf2"><tr><td align="center" style="padding:28px 12px">
        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#ffffff;border-radius:20px;overflow:hidden">
        <tr><td style="padding:25px 28px;border-bottom:1px solid #e1e8f0"><a href="{E(settings.FrontendUrl)}" style="font-size:19px;letter-spacing:2px;font-weight:bold;text-decoration:none;color:#243f70">☀ METEOROLOJİ</a><p style="margin:6px 0 0;font-size:9px;letter-spacing:3px;color:#617795">HAVA DURUMU</p></td></tr>
        <tr><td style="padding:26px 28px">{content}</td></tr>
        </table><p style="font-size:11px;color:#7b8b9e;margin:20px 0 0">Her yeni güne, hava durumunu bilerek başla.</p>
        </td></tr></table></body></html>
        """;
}
