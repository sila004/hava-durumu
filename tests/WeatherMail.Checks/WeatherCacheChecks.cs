using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Intern.API.Controllers;
using Intern.Service.Services.Concretes;
using Intern.Shared.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class WeatherCacheChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var forecast = (await new FakeWeather().GetForecastAsync("Ankara")).Data;
        var payload = JsonSerializer.Serialize(forecast, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var settings = Options.Create(new WeatherSettings
        {
            ForecastUrl = "https://forecast.example.test/forecast", AirQualityUrl = "https://air.example.test/air",
            CacheSeconds = 600, RateLimitBackoffSeconds = 300
        });
        var clock = new TestClock();
        using var cache = new WeatherRequestCache(clock);
        using var handler = new TestHandler(async (_, ct) =>
        {
            await Task.Delay(15, ct);
            return Json(payload);
        });
        var factory = new TestFactory(handler);
        WeatherService CreateService() => new(factory, settings, NullLogger<WeatherService>.Instance, cache);
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => CreateService().GetForecastAsync("Ankara")));
        check(replies.All(reply => reply.IsSuccess) && handler.Calls == 1, "Aynı şehir için eşzamanlı istekler tek sağlayıcı çağrısı yapar");
        var current = await CreateService().GetWeatherAsync("Ankara");
        check(current.IsSuccess && current.Data.Temperature == forecast.Current.Temperature && handler.Calls == 1,
            "Şehir kartı ve ayrıntılı tahmin aynı önbelleği paylaşır");
        await Task.WhenAll(CreateService().GetForecastAsync("Bursa"), CreateService().GetForecastAsync("İstanbul"));
        check(handler.Calls == 3 && handler.MaximumActive == 1, "Farklı şehirler ayrılır ve sağlayıcıya paralel yük gönderilmez");
        clock.Advance(TimeSpan.FromSeconds(601));
        await CreateService().GetForecastAsync("Ankara");
        check(handler.Calls == 4, "Süresi dolan tahmin sağlayıcıdan yenilenir");

        var limitClock = new TestClock();
        using var limitedCache = new WeatherRequestCache(limitClock);
        var forecastCalls = 0;
        using var limitedHandler = new TestHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "air.example.test")
                return Task.FromResult(Json("{\"current\":{\"time\":\"2026-11-10T07:00\",\"european_aqi\":10},\"current_units\":{}}"));
            if (Interlocked.Increment(ref forecastCalls) > 1) return Task.FromResult(Json(payload));
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return Task.FromResult(response);
        });
        var limited = new WeatherService(new TestFactory(limitedHandler), settings, NullLogger<WeatherService>.Instance, limitedCache);
        var first = await limited.GetForecastAsync("Ankara");
        var otherCity = await limited.GetForecastAsync("Bursa");
        check(first.StatusCode == 503 && first.Error.Code == "WeatherProviderRateLimited" && otherCity.StatusCode == 503 && forecastCalls == 1,
            "429 sonrası diğer şehirler de sağlayıcıya istek göndermeden bekler");
        var controller = new BaseController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var actionResult = controller.ActionResultInstance(first) as ObjectResult;
        check(actionResult?.StatusCode == 503 && controller.Response.Headers["Retry-After"] == "120",
            "API istek sınırını 503 ve Retry-After başlığıyla bildirir");
        var air = await limited.GetAirQualityAsync("Ankara");
        check(air.IsSuccess && air.Data.Aqi == 10, "Tahmin sınırı farklı sağlayıcı adresindeki hava kalitesini durdurmaz");
        limitClock.Advance(TimeSpan.FromSeconds(119));
        await limited.GetForecastAsync("İzmir");
        check(forecastCalls == 1, "Retry-After süresi dolmadan yeniden deneme yapılmaz");
        limitClock.Advance(TimeSpan.FromSeconds(1));
        var recovered = await limited.GetForecastAsync("Ankara");
        check(recovered.IsSuccess && forecastCalls == 2, "Bekleme süresi dolunca sağlayıcı yeniden denenir ve veri döner");

        using var defaultCache = new WeatherRequestCache(new TestClock());
        using var defaultHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var noHeaderService = new WeatherService(new TestFactory(defaultHandler), settings, NullLogger<WeatherService>.Instance, defaultCache);
        var noHeader = await noHeaderService.GetForecastAsync("Ankara");
        check(noHeader.Error.Params?["retryAfterSeconds"] == "300", "Retry-After verilmezse beş dakika bekleme uygulanır");

        var dateClock = new TestClock();
        using var dateCache = new WeatherRequestCache(dateClock);
        using var dateHandler = new TestHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(dateClock.GetUtcNow().AddMinutes(3));
            return Task.FromResult(response);
        });
        var dateService = new WeatherService(new TestFactory(dateHandler), settings, NullLogger<WeatherService>.Instance, dateCache);
        var dated = await dateService.GetForecastAsync("Ankara");
        check(dated.Error.Params?["retryAfterSeconds"] == "180", "HTTP tarihi biçimindeki Retry-After da korunur");

        using var invalidCache = new WeatherRequestCache(new TestClock());
        var invalidAttempts = 0;
        using var invalidHandler = new TestHandler((_, _) => Task.FromResult(Json(++invalidAttempts == 1 ? "{}" : payload)));
        var invalidService = new WeatherService(new TestFactory(invalidHandler), settings, NullLogger<WeatherService>.Instance, invalidCache);
        var invalid = await invalidService.GetForecastAsync("Ankara");
        var valid = await invalidService.GetForecastAsync("Ankara");
        check(invalid.StatusCode == 502 && valid.IsSuccess && invalidHandler.Calls == 2, "Eksik veya bozuk tahmin başarılı veri gibi önbelleğe alınmaz");
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 10, 7, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan interval) => now += interval;
    }

    private sealed class TestFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private int calls;
        private int active;
        private int maximumActive;
        public int Calls => calls;
        public int MaximumActive => maximumActive;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            var concurrent = Interlocked.Increment(ref active);
            int observed;
            do { observed = maximumActive; } while (concurrent > observed && Interlocked.CompareExchange(ref maximumActive, concurrent, observed) != observed);
            try { return await respond(request, cancellationToken); }
            finally { Interlocked.Decrement(ref active); }
        }
    }
}
