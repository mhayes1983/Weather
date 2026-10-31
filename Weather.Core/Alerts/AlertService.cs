using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Alerts;

public sealed class AlertService(
    StationDirectory directory,
    StationLocator locator,
    NwsAlerts alerts,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    ILogger<AlertService> logger)
{
    /// <summary>Returns null when the ID is badly formed.</summary>
    public async Task<AlertReport?> GetAsync(string? stationId = null)
    {
        var station = directory.Find(stationId);
        if (station is null)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            return new AlertReport(station.Name, SourceStatus.NeedsKey, "Needs key", []);
        }

        return await cache.GetOrCreateAsync(
            $"alerts:{station.Id}",
            async () =>
            {
                var ttl = TimeSpan.FromMinutes(options.Value.AlertCacheMinutes);

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var location = await locator.GetAsync(station, timeout.Token);
                    return (new AlertReport(station.Name, SourceStatus.Ok, null, await alerts.GetAsync(location, timeout.Token)), ttl);
                }
                catch (Exception exception)
                {
                    // Old alerts could be wrong, so a failure reports itself rather than showing last good data.
                    logger.LogWarning("Alerts for {Id} failed: {Error}", station.Id, exception.GetType().Name);
                    return (new AlertReport(station.Name, SourceStatus.Error, SourceFailure.Describe(exception), []), ttl);
                }
            });
    }
}
