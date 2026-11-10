using Intern.Core.Entities.Enums;

namespace Intern.Service.Services.Abstracts
{
    /// <summary>Güncel kur okuması. Ağ hatası hiçbir zaman çağıranı düşürmez.</summary>
    public record ExchangeRateResult(decimal? Rate, DateTime? FetchedAt, string? Warning);

    /// <summary>
    /// TCMB'nin anahtarsız günlük kur servisinden çapraz kur okur.
    /// </summary>
    public interface IExchangeRateService
    {
        /// <summary>
        /// 1 birim <paramref name="from"/> kaç birim <paramref name="to"/> eder.
        /// Okunamazsa Rate null döner ve Warning dolar; istek hata olarak bitmez.
        /// </summary>
        Task<ExchangeRateResult> GetRateAsync(CurrencyType from, CurrencyType to, CancellationToken ct = default);
    }
}
