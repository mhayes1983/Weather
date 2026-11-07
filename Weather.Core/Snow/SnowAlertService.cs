using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Alerts;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>
/// Winter alerts for the Snow Forecast page's towns, each listed once with the towns it covers.
/// Like other alerts, a failure is reported and never replaced by old data.
/// </summary>
public sealed class SnowAlertService(
    LocationResolver locationResolver,
    NwsAlerts alerts,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    ILogger<SnowAlertService> logger)
{
    private static readonly string[] WinterWords =
    [
        "winter", "blizzard", "snow", "ice storm", "freezing rain", "freezing fog", "freezing spray",
        "sleet", "wind chill", "extreme cold", "cold weather", "lake effect",
    ];

    public static bool IsWinter(string eventName) =>
        WinterWords.Any(word => eventName.Contains(word, StringComparison.OrdinalIgnoreCase));

    public async Task<SnowAlertReport> GetAsync(IReadOnlyList<string> zips)
    {
        var checks = (await Task.WhenAll(zips.Select(GetForZipAsync))).Select(check => check.Alerts).ToArray();

        var failed = zips.Where((_, i) => checks[i] is null).ToList();
        var found = new Dictionary<string, (WeatherAlert Alert, List<string> Zips)>();
        for (var i = 0; i < zips.Count; i++)
        {
            foreach (var alert in checks[i] ?? [])
            {
                if (!found.TryGetValue(alert.Id, out var entry))
                {
                    entry = (alert, []);
                    found[alert.Id] = entry;
                }

                entry.Zips.Add(zips[i]);
            }
        }

        var list = found.Values.Select(entry => new SnowAlert(entry.Alert, entry.Zips)).ToList();
        return failed.Count == 0
            ? new SnowAlertReport(SourceStatus.Ok, null, list)
            : new SnowAlertReport(
                SourceStatus.Error,
                failed.Count == zips.Count
                    ? "Alerts are unavailable right now."
                    : $"Alerts couldn't be checked for {string.Join(", ", failed)}.",
                list);
    }

    // Null means the check failed, which must never look like "no alerts".
    private Task<ZipAlerts> GetForZipAsync(string zip) =>
        cache.GetOrCreateAsync(
            $"snow-alerts:{zip}",
            async () =>
            {
                var ttl = TimeSpan.FromMinutes(options.Value.AlertCacheMinutes);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var location = await locationResolver.ResolveAsync(zip, timeout.Token);
                    var all = await alerts.GetAsync(new GeoPoint(location.Latitude, location.Longitude), timeout.Token);
                    return (new ZipAlerts(all.Where(alert => IsWinter(alert.Event)).ToList()), ttl);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Snow alerts for {Zip} failed: {Error}", zip, exception.GetType().Name);
                    return (new ZipAlerts(null), ttl);
                }
            });

    private sealed record ZipAlerts(IReadOnlyList<WeatherAlert>? Alerts);
}
