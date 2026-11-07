namespace Weather.Core.Sources;

/// <summary>
/// Open-Meteo answers a burst of calls with 429, so its models share one request per endpoint and
/// location, and only a few requests run at once across the temperature and snow tables.
/// </summary>
internal static class OpenMeteoRequests
{
    private static readonly SemaphoreSlim Concurrency = new(3);

    public static Task<string> GetSharedAsync(
        IHttpClientFactory httpClientFactory,
        WeatherCache cache,
        string cacheKey,
        string url,
        TimeSpan cacheDuration,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(cacheKey, async () =>
        {
            await Concurrency.WaitAsync(cancellationToken);
            try
            {
                var json = await httpClientFactory.CreateClient(WeatherHttp.ClientName).GetStringAsync(url, cancellationToken);
                return (json, cacheDuration);
            }
            finally
            {
                Concurrency.Release();
            }
        });
}
