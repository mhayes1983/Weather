using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Weather.Core.Climate;

/// <summary>No ACIS station within the widest search meets <see cref="ClimateStations"/>' rule.</summary>
public sealed class NoClimateStationException() : Exception("No active climate station with normals and 30 years of records is nearby.");

/// <summary>
/// Chooses one ACIS station for a location, shared by the forecast table's averages and the almanac so they
/// always agree. It's the nearest station that still reports, has kept records for at least
/// <see cref="MinimumRecordYears"/> years and has normals, so a closed station such as Brunswick is never used.
/// </summary>
public sealed class ClimateStations(IHttpClientFactory httpClientFactory, WeatherCache cache, IOptions<WeatherOptions> options)
{
    public const int MinimumRecordYears = 30;

    // Co-op observers can report a few days late, so a station this recent still counts as active.
    private const int ActiveWithinDays = 7;

    private static readonly double[] SearchRadiiDegrees = [0.3, 0.75, 1.5, 3];
    private static readonly TimeSpan StationCacheDuration = TimeSpan.FromDays(30);

    // Having no station rarely changes, so the four searches aren't repeated every few minutes.
    private static readonly TimeSpan NoStationCacheDuration = TimeSpan.FromDays(1);

    /// <exception cref="NoClimateStationException">No suitable station is nearby.</exception>
    internal async Task<AcisStation> FindAsync(double latitude, double longitude, DateOnly today)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"climate-station:{latitude:F3},{longitude:F3}");
        var station = await cache.GetOrCreateAsync<AcisStation?>(key, async () =>
        {
            var found = await Acis.WithTimeoutAsync(
                TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds),
                token => SearchAsync(latitude, longitude, today, token));
            return (found, found is null ? NoStationCacheDuration : StationCacheDuration);
        });

        return station ?? throw new NoClimateStationException();
    }

    private async Task<AcisStation?> SearchAsync(double latitude, double longitude, DateOnly today, CancellationToken cancellationToken)
    {
        var yesterday = Acis.Date(today.AddDays(-1));
        foreach (var radius in SearchRadiiDegrees)
        {
            var body = new JsonObject
            {
                ["bbox"] = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{longitude - radius},{latitude - radius},{longitude + radius},{latitude + radius}"),
                ["sdate"] = yesterday,
                ["edate"] = yesterday,
                ["elems"] = new JsonArray(
                    new JsonObject { ["name"] = "maxt" },
                    new JsonObject { ["name"] = "mint" },
                    Acis.NormalElement("maxt"),
                    Acis.NormalElement("mint")),
                ["meta"] = "name,sids,ll,valid_daterange",
            };

            using var document = await Acis.PostAsync(httpClientFactory, "MultiStnData", body, cancellationToken);
            var nearest = document.RootElement.GetProperty("data").EnumerateArray()
                .Where(station => IsSuitable(station, today))
                .Select(Acis.ToStation)
                .MinBy(station => Acis.DistanceSquared(latitude, longitude, station));

            if (nearest is not null)
            {
                return nearest;
            }
        }

        return null;
    }

    // Normals come back in the data, and the measured highs' and lows' periods in valid_daterange.
    // Both normals are needed, so a station with only highs doesn't leave the lows unhighlighted.
    internal static bool IsSuitable(JsonElement station, DateOnly today)
    {
        var meta = station.GetProperty("meta");
        var data = station.GetProperty("data");
        if (!meta.TryGetProperty("ll", out _)
            || data.GetArrayLength() < 4 || !Acis.IsPresent(data[2]) || !Acis.IsPresent(data[3])
            || !meta.TryGetProperty("valid_daterange", out var ranges) || ranges.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var startedBy = today.AddYears(-MinimumRecordYears);
        var activeSince = today.AddDays(-ActiveWithinDays);
        return ranges.GetArrayLength() >= 2 && Enumerable.Range(0, 2).All(index =>
            ranges[index] is { ValueKind: JsonValueKind.Array } range && range.GetArrayLength() == 2
            && Acis.ParseDate(range[0]) is { } start && start <= startedBy
            && Acis.ParseDate(range[1]) is { } end && end >= activeSince);
    }
}
