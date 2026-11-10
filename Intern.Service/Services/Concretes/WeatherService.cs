

//Şehir adı gönderilmiş mi diye bakıyor.
//Şehri kendi listesinden buluyor.
//Şehrin enlem ve boylamını alıyor.
//İstenen hava alanlarıyla Open-Meteo adresini oluşturuyor.
//HTTP isteğini gönderiyor.
//Gelen JSON’u C# veri sınıflarına dönüştürüyor.
//Başarı veya hata cevabı hazırlıyor.


using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Intern.Entities.DTOs.Weather;
using Intern.Service.Services.Abstracts;
using Intern.Shared.DTOs;
using Intern.Shared.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Intern.Service.Services.Concretes;

public class WeatherService : IWeatherService
{
    public const string HttpClientName = "OpenMeteo";

    // Her iki ön yüz listesindeki şehirler. Koordinatlar sunucuda doğrulanır.
    private static readonly (string Name, double Lat, double Lon)[] Cities =
    [
        ("İstanbul", 41.01, 28.98), ("Ankara", 39.93, 32.86), ("İzmir", 38.42, 27.14),
        ("Bursa", 40.18, 29.06), ("Antalya", 36.90, 30.70), ("Adana", 37.00, 35.32),
        ("Konya", 37.87, 32.49), ("Gaziantep", 37.07, 37.38), ("Trabzon", 41.00, 39.72),
        ("Samsun", 41.29, 36.33), ("Kayseri", 38.73, 35.49), ("Eskişehir", 39.77, 30.52),
        ("Diyarbakır", 37.91, 40.23), ("Van", 38.50, 43.38), ("Erzurum", 39.91, 41.27),
        ("Balıkesir", 39.65, 27.89), ("Çanakkale", 40.15, 26.41), ("Kocaeli", 40.77, 29.94),
        ("Muğla", 37.22, 28.36), ("Rize", 41.02, 40.52), ("Sivas", 39.75, 37.02)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    public static IReadOnlyList<string> CityNames => Cities.Select(city => city.Name).ToArray();
    private readonly WeatherSettings _settings;
    private readonly ILogger<WeatherService> _logger;
    private readonly WeatherRequestCache _cache;

    public WeatherService(IHttpClientFactory httpClientFactory, IOptions<WeatherSettings> settings,
        ILogger<WeatherService> logger, WeatherRequestCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
        _cache = cache;
    }

    public async Task<Response<WeatherDto>> GetWeatherAsync(string? city, CancellationToken ct = default)
    {
        var result = await GetForecastAsync(city, ct);
        if (!result.IsSuccess)
            return Response<WeatherDto>.Fail(result.Error, result.StatusCode);

        var current = result.Data.Current;
        return Response<WeatherDto>.Success(new WeatherDto
        {
            Temperature = current.Temperature, Humidity = current.Humidity,
            Apparent = current.Apparent, Wind = current.Wind, Code = current.Code
        }, 200);
    }

    public Task<Response<ForecastDto>> GetForecastAsync(string? city, CancellationToken ct = default,
        double? latitude = null, double? longitude = null) =>
        FetchAsync<ForecastDto>(city, _settings.ForecastUrl,
            "current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,wind_direction_10m,surface_pressure,precipitation,cloud_cover,weather_code,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset" +
            "&hourly=temperature_2m,apparent_temperature,weather_code,precipitation_probability,precipitation,wind_speed_10m,relative_humidity_2m,is_day,uv_index&forecast_days=7",
            data => data.Current is not null && data.Daily is not null && data.Hourly is not null &&
                data.Daily.Time is { Length: > 0 } && data.Hourly.Time is { Length: > 0 },
            "Hava durumu alınamadı.", ct, latitude, longitude);

    public async Task<Response<AirQualityDto>> GetAirQualityAsync(string? city, CancellationToken ct = default)
    {
        var result = await GetAirQualityDetailAsync(city, ct);
        if (!result.IsSuccess)
            return Response<AirQualityDto>.Fail(result.Error, result.StatusCode);

        var current = result.Data.Current;
        return Response<AirQualityDto>.Success(new AirQualityDto
        {
            Aqi = current.Aqi, Pm25 = current.Pm25, Pm10 = current.Pm10,
            Ozone = current.Ozone, NitrogenDioxide = current.NitrogenDioxide
        }, 200);
    }

    public Task<Response<AirQualityDetailDto>> GetAirQualityDetailAsync(string? city, CancellationToken ct = default,
        double? latitude = null, double? longitude = null) =>
        FetchAsync<AirQualityDetailDto>(city, _settings.AirQualityUrl,
            "current=european_aqi,pm2_5,pm10,ozone,nitrogen_dioxide,carbon_monoxide,sulphur_dioxide",
            data => data.Current is not null && data.CurrentUnits is not null,
            "Hava kalitesi alınamadı.", ct, latitude, longitude);

    private async Task<Response<T>> FetchAsync<T>(string? cityName, string url, string fields,
        Func<T, bool> isValid, string errorMessage, CancellationToken ct,
        double? latitude = null, double? longitude = null) where T : class
    {
        var useCoordinates = latitude.HasValue || longitude.HasValue;
        string locationName;
        double lat;
        double lon;
        if (useCoordinates)
        {
            if (!latitude.HasValue || !longitude.HasValue ||
                !double.IsFinite(latitude.Value) || !double.IsFinite(longitude.Value) ||
                latitude.Value is < -90 or > 90 || longitude.Value is < -180 or > 180)
                return Response<T>.Fail("InvalidCoordinates",
                    "Geçerli bir enlem (-90 ile 90) ve boylam (-180 ile 180) birlikte gönderilmeli.", 400, true);

            lat = Math.Round(latitude.Value, 3);
            lon = Math.Round(longitude.Value, 3);
            locationName = "Konumum";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(cityName))
                return Response<T>.Fail("CityRequired", "Şehir adı veya konum koordinatları gerekli.", 400, true);

            var city = Cities.FirstOrDefault(item =>
                string.Equals(item.Name.ToLower(TurkishCulture), cityName.Trim().ToLower(TurkishCulture),
                    StringComparison.Ordinal));
            if (city.Name is null)
                return Response<T>.Fail("CityNotFound", "Şehir bulunamadı.", 404, true);

            (locationName, lat, lon) = city;
        }

        var coordinates = FormattableString.Invariant($"latitude={lat}&longitude={lon}");
        var timezone = useCoordinates ? "auto" : "Europe%2FIstanbul";
        var requestUrl = $"{url}?{coordinates}&{fields}&timezone={timezone}";
        var authority = new Uri(url).GetLeftPart(UriPartial.Authority);

        ct.ThrowIfCancellationRequested();
        if (_cache.TryGet<T>(requestUrl, out var cached)) return Response<T>.Success(cached!, 200);
        // Aynı anda açılan kartlar ve farklı kullanıcılar sağlayıcıya paralel yük bindirmez.
        await _cache.RequestGate.WaitAsync(ct);
        try
        {
            if (_cache.TryGet<T>(requestUrl, out cached)) return Response<T>.Success(cached!, 200);
            var remaining = _cache.RemainingBackoff(authority);
            if (remaining > TimeSpan.Zero) return RateLimited<T>(remaining);

            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(requestUrl, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter;
                var wait = retryAfter?.Delta ?? (retryAfter?.Date - _cache.Now) ??
                    TimeSpan.FromSeconds(Math.Max(1, _settings.RateLimitBackoffSeconds));
                if (wait <= TimeSpan.Zero) wait = TimeSpan.FromSeconds(Math.Max(1, _settings.RateLimitBackoffSeconds));
                _cache.SetBackoff(authority, wait);
                _logger.LogWarning("Open-Meteo istek sınırı. {Seconds} saniye boyunca yeni sağlayıcı isteği yapılmayacak.", Math.Ceiling(wait.TotalSeconds));
                return RateLimited<T>(wait);
            }
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<T>(ct);
            if (data is null || !isValid(data))
                throw new JsonException("Dış API yanıtındaki gerekli alanlar eksik.");

            _cache.Store(requestUrl, data, TimeSpan.FromSeconds(Math.Clamp(_settings.CacheSeconds, 1, 3600)));
            return Response<T>.Success(data, 200);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Open-Meteo verisi alınamadı. Konum: {Location}", locationName);
            return Response<T>.Fail("WeatherProviderUnavailable", errorMessage, 502, true);
        }
        finally { _cache.RequestGate.Release(); }
    }

    private static Response<T> RateLimited<T>(TimeSpan wait) where T : class => Response<T>.Fail(
        "WeatherProviderRateLimited",
        "Hava verisi sağlayıcısının istek sınırına ulaşıldı. Lütfen biraz sonra tekrar deneyin.",
        new Dictionary<string, string> { ["retryAfterSeconds"] = Math.Ceiling(wait.TotalSeconds).ToString(CultureInfo.InvariantCulture) },
        503, true);

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
}
