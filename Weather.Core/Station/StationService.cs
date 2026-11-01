using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class StationService(
    StationDirectory directory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    StationSky skies,
    ILogger<StationService> logger)
{
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);

    public IReadOnlyList<StationSummary> Stations => directory.Stations;

    /// <summary>Returns null when the ID is badly formed.</summary>
    public async Task<StationReport?> GetReportAsync(string? id = null)
    {
        var station = directory.Find(id);
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
            && now - reading.ObservedAt > station.OfflineAfter;
        var status = current.Message is not null ? StationStatus.Error
            : offline ? StationStatus.Offline
            : StationStatus.Ok;

        // A description of the sky now shouldn't sit beside readings that are hours old.
        var sky = status == StationStatus.Ok ? await skies.GetAsync(station) : null;

        return new StationReport(
            status,
            offline ? "The station has stopped reporting." : current.Message,
            now,
            station.Source,
            current.Value,
            history.Value ?? [],
            station.HistoryRange,
            history.Message,
            station.Modelled,
            station.HistoryMaxGapMinutes,
            sky);
    }

    /// <summary>The latest reading, from the same cache as the station's page.</summary>
    /// <exception cref="Exception">The failure that stopped the reading being fetched.</exception>
    public async Task<StationReading> GetCurrentAsync(IWeatherStation station, CancellationToken cancellationToken)
    {
        var current = await GetAsync(station, "current", station.CurrentCacheDuration, station.GetCurrentAsync)
            .WaitAsync(cancellationToken);

        return current.Failure is null ? current.Value! : throw current.Failure;
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
                    return (new Fetched<T>(value, null, null), ttl);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Station {Id} {Name} failed: {Error}", station.Id, name, exception.GetType().Name);

                    var message = SourceFailure.Describe(exception);
                    var fetched = cache.TryGet<T>(lastGoodKey, out var lastGood)
                        ? new Fetched<T>(lastGood, $"Showing last good data. {message}", exception)
                        : new Fetched<T>(null, message, exception);
                    return (fetched, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private sealed record Fetched<T>(T? Value, string? Message, Exception? Failure);
}
