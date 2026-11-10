namespace Intern.Shared.Settings
{
    /// <summary>appsettings.json → "ExchangeRate" bölümü. IOptions&lt;ExchangeRateSettings&gt; ile okunur.</summary>
    public class ExchangeRateSettings
    {
        public const string SectionName = "ExchangeRate";
        /// <summary>TCMB'nin anahtarsız günlük kur XML adresi.</summary>
        public string TodayUrl { get; set; } = "https://www.tcmb.gov.tr/kurlar/today.xml";
        /// <summary>Tek denemenin zaman aşımı (saniye).</summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}
