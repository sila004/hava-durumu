using Intern.Service.Services.Abstracts;
using Microsoft.AspNetCore.Mvc;

namespace Intern.API.Controllers;

[ApiController]
[Route("api/hava-durumu")]
public class WeatherController : BaseController
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService) => _weatherService = weatherService;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? city, CancellationToken ct) =>
        ActionResultInstance(await _weatherService.GetWeatherAsync(city, ct));

    [HttpGet("tahmin")]
    public async Task<IActionResult> GetForecast([FromQuery] string? city,
        [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken ct) =>
        ActionResultInstance(await _weatherService.GetForecastAsync(city, ct, latitude, longitude));
}
