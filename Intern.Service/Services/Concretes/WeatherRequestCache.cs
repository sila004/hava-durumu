using Microsoft.Extensions.Caching.Memory;

namespace Intern.Service.Services.Concretes;

// API istekleri ve günlük mail işçisi aynı önbelleği/bekleme durumunu paylaşır.
public sealed class WeatherRequestCache(TimeProvider clock) : IDisposable
{
    private readonly MemoryCache values = new(new MemoryCacheOptions { SizeLimit = 256 });
    public SemaphoreSlim RequestGate { get; } = new(1, 1);
    public DateTimeOffset Now => clock.GetUtcNow();

    public bool TryGet<T>(string url, out T? data) where T : class
    {
        if (values.TryGetValue((typeof(T), url), out CachedValue<T>? entry) && entry is not null && entry.Until > Now)
        {
            data = entry.Data;
            return true;
        }
        data = null;
        return false;
    }

    public void Store<T>(string url, T data, TimeSpan duration) where T : class =>
        values.Set((typeof(T), url), new CachedValue<T>(data, Now.Add(duration)),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration, Size = 1 });

    public TimeSpan RemainingBackoff(string authority) =>
        values.TryGetValue(("backoff", authority), out DateTimeOffset until) && until > Now
            ? until - Now : TimeSpan.Zero;

    public void SetBackoff(string authority, TimeSpan duration) =>
        values.Set(("backoff", authority), Now.Add(duration),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration, Size = 1, Priority = CacheItemPriority.NeverRemove });

    public void Dispose()
    {
        values.Dispose();
        RequestGate.Dispose();
    }

    private sealed record CachedValue<T>(T Data, DateTimeOffset Until);
}
