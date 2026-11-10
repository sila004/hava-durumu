using Intern.Core.Entities.Enums;
using Intern.Entities.DTOs.ExchangeRate;
using Intern.Service.Services.Abstracts;
using Intern.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Controllers
{
    /// <summary>
    /// Dış API örneği: kur okunamazsa yine 200, Rate null + Warning.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRateController : BaseController
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        [HttpGet]
        public async Task<IActionResult> GetRate(
            [FromQuery] CurrencyType from,
            [FromQuery] CurrencyType to,
            CancellationToken cancellationToken)
        {
            var result = await _exchangeRateService.GetRateAsync(from, to, cancellationToken);

            return ActionResultInstance(Response<ExchangeRateDto>.Success(new ExchangeRateDto
            {
                From = from,
                To = to,
                Rate = result.Rate,
                FetchedAt = result.FetchedAt,
                Warning = result.Warning
            }, 200));
        }
    }
}
