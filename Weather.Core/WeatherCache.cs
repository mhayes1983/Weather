using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Weather.Core;

/// <summary>
/// Size-limited in-memory cache. Concurrent callers for one key share a single
/// upstream request, and each result chooses how long it is kept.
/// </summary>
public sealed class WeatherCache : IDisposable
{
    private const int MaxEntries = 2000;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inFlight = new();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<(T Value, TimeSpan Ttl)>> factory)
    {
        if (TryGet(key, out T? cached))
        {
            return cached!;
        }

        var pending = _inFlight.GetOrAdd(key, _ => new Lazy<Task<object?>>(() => LoadAsync(key, factory)));
        return (T)(await pending.Value)!;
    }

    public bool TryGet<T>(string key, out T? value) => _cache.TryGetValue(key, out value);

    public void Set<T>(string key, T value, TimeSpan ttl) =>
        _cache.Set(key, value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = ttl });

    public void Remove(string key) => _cache.Remove(key);

    public void Dispose() => _cache.Dispose();

    private async Task<object?> LoadAsync<T>(string key, Func<Task<(T Value, TimeSpan Ttl)>> factory)
    {
        try
        {
            var (value, ttl) = await factory();
            Set(key, value, ttl);
            return value;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }
}
