using Intern.Entities.DTOs.Weather;
using Intern.Shared.DTOs;

namespace Intern.Service.Services.Abstracts;

public interface IWeatherService
{
    Task<Response<WeatherDto>> GetWeatherAsync(string? city, CancellationToken ct = default);
    Task<Response<ForecastDto>> GetForecastAsync(string? city, CancellationToken ct = default,
        double? latitude = null, double? longitude = null);
    Task<Response<AirQualityDto>> GetAirQualityAsync(string? city, CancellationToken ct = default);
    Task<Response<AirQualityDetailDto>> GetAirQualityDetailAsync(string? city, CancellationToken ct = default,
        double? latitude = null, double? longitude = null);
}
