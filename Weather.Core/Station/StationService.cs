using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class StationService(
    IEnumerable<IWeatherStation> stations,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<StationService> logger)
{
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);

    // Registration order, with the home station first.
    public IReadOnlyList<StationSummary> Stations { get; } =
        stations.Select(station => new StationSummary(station.Id, station.Name)).ToList();

    /// <summary>Returns null when the ID isn't a configured station.</summary>
    public async Task<StationReport?> GetReportAsync(string? id = null)
    {
        var station = stations.FirstOrDefault(station =>
            string.Equals(station.Id, id ?? AmbientWeatherStation.HomeId, StringComparison.OrdinalIgnoreCase));
        if (station is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        if (!station.HasKeys)
        {
            return new StationReport(StationStatus.NeedsKey, "Needs key", now, station.Source, null, [], station.HistoryRange, null);
        }

        var current = await GetAsync(station, "current", station.CurrentCacheDuration, station.GetCurrentAsync);
        var history = await GetAsync(station, "history", station.HistoryCacheDuration, station.GetHistoryAsync);

        var offline = current is { Message: null, Value: { } reading }
            && now - reading.ObservedAt > TimeSpan.FromMinutes(options.Value.AmbientWeather.OfflineAfterMinutes);
        var status = current.Message is not null ? StationStatus.Error
            : offline ? StationStatus.Offline
            : StationStatus.Ok;

        return new StationReport(
            status,
            offline ? "The station has stopped reporting." : current.Message,
            now,
            station.Source,
            current.Value,
            history.Value ?? [],
            station.HistoryRange,
            history.Message);
    }

    private Task<Fetched<T>> GetAsync<T>(
        IWeatherStation station,
        string name,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> fetch) where T : class =>
        cache.GetOrCreateAsync(
            $"station:{station.Id}:{name}",
            async () =>
            {
                var lastGoodKey = $"last-good:station:{station.Id}:{name}";

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
                    logger.LogWarning("Station {Id} {Name} failed: {Error}", station.Id, name, exception.GetType().Name);

                    var message = SourceFailure.Describe(exception);
                    var fetched = cache.TryGet<T>(lastGoodKey, out var lastGood)
                        ? new Fetched<T>(lastGood, $"Showing last good data. {message}")
                        : new Fetched<T>(null, message);
                    return (fetched, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private sealed record Fetched<T>(T? Value, string? Message);
}
