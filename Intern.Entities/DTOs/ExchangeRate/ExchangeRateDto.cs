using Intern.Core.Entities.Enums;

namespace Intern.Entities.DTOs.ExchangeRate
{
    /// <summary>
    /// 1 birim <see cref="From"/> kaç birim <see cref="To"/> eder.
    /// Kur okunamadıysa <see cref="Rate"/> null gelir ve <see cref="Warning"/> dolar;
    /// istek yine başarılıdır, kullanıcı kuru elle girer.
    /// </summary>
    public class ExchangeRateDto
    {
        public CurrencyType From { get; set; }
        public CurrencyType To { get; set; }
        public decimal? Rate { get; set; }
        public DateTime? FetchedAt { get; set; }
        public string? Warning { get; set; }
    }
}
