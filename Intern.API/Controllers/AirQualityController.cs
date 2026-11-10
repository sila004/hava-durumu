using Intern.Service.Services.Abstracts;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Controllers;

[ApiController]
[Route("api/hava-kalitesi")] //--> bu controllera bu adresten ulaşılabilir demek
public class AirQualityController : BaseController
{
    private readonly IWeatherService _weatherService;

    public AirQualityController(IWeatherService weatherService) => _weatherService = weatherService;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? city, CancellationToken ct) =>
        ActionResultInstance(await _weatherService.GetAirQualityAsync(city, ct));

    [HttpGet("detay")]
    public async Task<IActionResult> GetDetail([FromQuery] string? city,
        [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken ct) =>
        ActionResultInstance(await _weatherService.GetAirQualityDetailAsync(city, ct, latitude, longitude));
}

// GetAirQualityAsync: işi yapması için servisi çağırır.

// ActionResultInstance: servis sonucunu uygun HTTP durum koduyla döndürür. 

