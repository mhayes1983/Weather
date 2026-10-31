using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class StationService(
    IWeatherStation station,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<StationService> logger)
{
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);

    public async Task<StationReport> GetReportAsync()
    {
        var now = timeProvider.GetUtcNow();
        if (!station.HasKeys)
        {
            return new StationReport(StationStatus.NeedsKey, "Needs key", now, null, [], null);
        }

        var settings = options.Value.AmbientWeather;
        var current = await GetAsync("current", TimeSpan.FromSeconds(settings.CurrentCacheSeconds), station.GetCurrentAsync);
        var history = await GetAsync("history", TimeSpan.FromMinutes(settings.HistoryCacheMinutes), station.GetHistoryAsync);

        var offline = current is { Message: null, Value: { } reading }
            && now - reading.ObservedAt > TimeSpan.FromMinutes(settings.OfflineAfterMinutes);
        var status = current.Message is not null ? StationStatus.Error
            : offline ? StationStatus.Offline
            : StationStatus.Ok;

        return new StationReport(
            status,
            offline ? "The station has stopped reporting." : current.Message,
            now,
            current.Value,
            history.Value ?? [],
            history.Message);
    }

    private Task<Fetched<T>> GetAsync<T>(string name, TimeSpan ttl, Func<CancellationToken, Task<T>> fetch) where T : class =>
        cache.GetOrCreateAsync(
            $"station:{name}",
            async () =>
            {
                var lastGoodKey = $"last-good:station:{name}";

                // The result is shared through the cache, so this has its own timeout.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var value = await fetch(timeout.Token);
                    cache.Set(lastGoodKey, value, LastGoodDuration);
                    return (new Fetched<T>(value, null), ttl);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Station {Name} failed: {Error}", name, exception.GetType().Name);

                    var message = SourceFailure.Describe(exception);
                    var fetched = cache.TryGet<T>(lastGoodKey, out var lastGood)
                        ? new Fetched<T>(lastGood, $"Showing last good data. {message}")
                        : new Fetched<T>(null, message);
                    return (fetched, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private sealed record Fetched<T>(T? Value, string? Message);
}
