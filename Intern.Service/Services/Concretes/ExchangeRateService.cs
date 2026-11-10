using Intern.Core.Entities.Enums;
using Intern.Service.Services.Abstracts;
using Intern.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Xml.Linq;

namespace Intern.Service.Services.Concretes
{
    /// <summary>
    /// TCMB günlük kur XML'ini okur. Dış API çağıran servis örneği.
    /// Ağ hatası çağıranı düşürmez: Rate null + Warning döner.
    /// </summary>
    public class ExchangeRateService : IExchangeRateService
    {
        /// <summary>IHttpClientFactory'de kayıtlı istemci adı (Program.cs ile aynı olmalı).</summary>
        public const string HttpClientName = "TcmbExchangeRate";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ExchangeRateSettings _settings;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(
            IHttpClientFactory httpClientFactory,
            IOptions<ExchangeRateSettings> settings,
            ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<ExchangeRateResult> GetRateAsync(
            CurrencyType from, CurrencyType to, CancellationToken ct = default)
        {
            if (from == to)
                return new ExchangeRateResult(1m, null, null);

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var xml = await client.GetStringAsync(_settings.TodayUrl, ct);
                var doc = XDocument.Parse(xml);

                // TCMB tüm kurları TRY cinsinden verir. Çapraz kur = fromTry / toTry.
                var fromTry = ReadTryRate(doc, from);
                var toTry = ReadTryRate(doc, to);

                if (fromTry == null || toTry == null || toTry.Value == 0m)
                    return new ExchangeRateResult(null, null, "Para birimi TCMB listesinde bulunamadı.");

                var rate = Math.Round(fromTry.Value / toTry.Value, 4, MidpointRounding.AwayFromZero);
                return new ExchangeRateResult(rate, DateTime.UtcNow, null);
            }
            catch (Exception ex)
            {
                // Ağ hatası çağıranı düşürmez: Rate null + Warning döner.
                _logger.LogWarning(ex, "TCMB kur servisine erişilemedi. {From}->{To}", from, to);
                return new ExchangeRateResult(null, null, "Güncel kur alınamadı.");
            }
        }

        /// <summary>1 birim para biriminin TRY karşılığı. TRY için 1.</summary>
        private static decimal? ReadTryRate(XDocument doc, CurrencyType currency)
        {
            if (currency == CurrencyType.TRY) return 1m;

            var node = doc.Root?
                .Elements("Currency")
                .FirstOrDefault(c => string.Equals((string?)c.Attribute("CurrencyCode"),
                    currency.ToString(), StringComparison.OrdinalIgnoreCase));

            if (node == null) return null;

            // ForexSelling boşsa (bazı para birimlerinde olur) BanknoteSelling'e düş.
            var raw = (string?)node.Element("ForexSelling");
            if (string.IsNullOrWhiteSpace(raw))
                raw = (string?)node.Element("BanknoteSelling");
            if (string.IsNullOrWhiteSpace(raw)) return null;

            if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return null;

            // "Unit" birden büyük olabilir (örn. 100 JPY). Birim başına düşürülür.
            var unitRaw = (string?)node.Element("Unit");
            if (decimal.TryParse(unitRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var unit) && unit > 0)
                value /= unit;

            return value;
        }
    }
}
