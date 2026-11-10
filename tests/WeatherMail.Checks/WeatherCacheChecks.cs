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
            CacheSeconds = 600, RateLimitBackoffSeconds = 300, EnableForecastFallback = false
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

        var fallbackPayload = FallbackPayload();
        using var fallbackDocument = JsonDocument.Parse(fallbackPayload);
        var mapped = VisualCrossingForecast.Parse(fallbackDocument.RootElement);
        check(mapped.Daily.Time.Length == 7 && mapped.Hourly.Time.Length == 168 && mapped.Current.Time == "2026-11-10T12:00",
            "Yedek sağlayıcı yedi günü, 168 saati ve yerel güncel zamanı korur");
        check(mapped.Current.Pressure is null && mapped.Hourly.UvIndex.All(value => value is null),
            "Yedek sağlayıcıda olmayan yüzey basıncı ve UV verisi sıfır diye gösterilmez");
        var missingRainJson = System.Text.Json.Nodes.JsonNode.Parse(fallbackPayload)!;
        missingRainJson["currentConditions"]!["precip"] = null;
        using var missingRainDocument = JsonDocument.Parse(missingRainJson.ToJsonString());
        var missingRain = VisualCrossingForecast.Parse(missingRainDocument.RootElement);
        check(missingRain.Current.Precipitation is null && missingRain.Current.Temperature == mapped.Current.Temperature && missingRain.Daily.Time.Length == 7,
            "Güncel yağış miktarı boş geldiğinde diğer hava verileri ve yedi günlük tahmin kaybolmaz");
        check(mapped.Current.IsDay == 1 && mapped.Hourly.IsDay[0] == 0 && mapped.Hourly.IsDay[12] == 1,
            "Yedek sağlayıcının gece/gündüz bilgisi gün doğumu ve batımına göre hesaplanır");
        var limitedHoursJson = System.Text.Json.Nodes.JsonNode.Parse(fallbackPayload)!;
        foreach (var day in limitedHoursJson["days"]!.AsArray().Skip(2)) day!.AsObject().Remove("hours");
        using var limitedHoursDocument = JsonDocument.Parse(limitedHoursJson.ToJsonString());
        var limitedHours = VisualCrossingForecast.Parse(limitedHoursDocument.RootElement);
        check(limitedHours.Daily.Time.Length == 7 && limitedHours.Hourly.Time.Length == 48,
            "Saatlik erişim ilk iki günle sınırlı olsa da yedi günlük tahmin korunur; eksik saatler uydurulmaz");

        var fallbackSettings = Options.Create(new WeatherSettings
        {
            ForecastUrl = "https://forecast.example.test/forecast",
            FallbackForecastUrl = "https://fallback.example.test/timeline"
        });
        using var fallbackCache = new WeatherRequestCache(new TestClock());
        var primaryCalls = 0;
        var fallbackCalls = 0;
        var requestedDisplayFields = false;
        using var fallbackHandler = new TestHandler((request, _) =>
        {
            if (request.RequestUri!.Host == "fallback.example.test")
            {
                requestedDisplayFields = request.RequestUri.Query.Contains("elements=") && request.RequestUri.Query.Contains("sunrise,sunset,uvindex");
                fallbackCalls++;
                return Task.FromResult(Json(fallbackPayload));
            }
            primaryCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(12)) } });
        });
        var fallbackService = new WeatherService(new TestFactory(fallbackHandler), fallbackSettings,
            NullLogger<WeatherService>.Instance, fallbackCache);
        var fallbackResponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fallbackService.GetForecastAsync("Ankara")));
        check(fallbackResponses.All(item => item.IsSuccess) && primaryCalls == 1 && fallbackCalls == 1,
            "Ana sağlayıcı 429 döndürürse yedek sağlayıcı bir kez çağrılır ve eşzamanlı istekler önbelleği kullanır");
        check(requestedDisplayFields, "Gün doğumu, gün batımı ve UV alanları sağlayıcının değişebilen varsayılanlarına bırakılmaz");
        var fallbackCard = await fallbackService.GetWeatherAsync("Ankara");
        check(fallbackCard.IsSuccess && fallbackCard.Data.Temperature == mapped.Current.Temperature && fallbackCalls == 1,
            "Şehir kartı, karşılaştırma ve ayrıntılı tahmin aynı yedek veriyi kullanır");
        var beforeInvalidCity = fallbackHandler.Calls;
        var invalidCity = await fallbackService.GetForecastAsync("Atlantis");
        check(invalidCity.StatusCode == 404 && fallbackHandler.Calls == beforeInvalidCity,
            "Geçersiz şehir yedek sağlayıcıya gönderilmez");

        using var bothLimitedCache = new WeatherRequestCache(new TestClock());
        using var bothLimitedHandler = new TestHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90)) } }));
        var bothLimited = new WeatherService(new TestFactory(bothLimitedHandler), fallbackSettings,
            NullLogger<WeatherService>.Instance, bothLimitedCache);
        var bothLimitedResponse = await bothLimited.GetForecastAsync("Ankara");
        await bothLimited.GetForecastAsync("Bursa");
        check(bothLimitedResponse.StatusCode == 503 && bothLimitedResponse.Error.Params["retryAfterSeconds"] == "90" && bothLimitedHandler.Calls == 2,
            "Her iki sağlayıcının istek sınırı da korunur; şehir değiştirerek tekrar istek gönderilmez");
    }

    private static string FallbackPayload()
    {
        var days = Enumerable.Range(0, 7).Select(index => new
        {
            datetime = new DateOnly(2026, 11, 10).AddDays(index).ToString("yyyy-MM-dd"),
            tempmax = 20, tempmin = 10, precipprob = 20,
            sunrise = "07:00:00", sunset = "17:00:00", icon = "partly-cloudy-day", conditions = "Partially cloudy",
            hours = Enumerable.Range(0, 24).Select(hour => new
            {
                datetime = $"{hour:D2}:00:00", temp = 15, feelslike = 14, humidity = 65, windspeed = 8,
                precipprob = 20, precip = 0, icon = "partly-cloudy-day", conditions = "Partially cloudy", uvindex = (double?)null
            })
        });
        return JsonSerializer.Serialize(new
        {
            timezone = "Europe/Istanbul", tzoffset = 3,
            currentConditions = new
            {
                datetimeEpoch = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
                temp = 15, feelslike = 14, humidity = 65, windspeed = 8, winddir = 40,
                precip = 0, cloudcover = 20, pressure = 1013, icon = "partly-cloudy-day", conditions = "Partially cloudy"
            },
            days
        });
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
