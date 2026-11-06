using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Climate;

/// <summary>Daily normal highs and lows from the location's climate station, the same one the almanac uses.</summary>
public sealed class AcisClimateNormals(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    ClimateStations stations,
    IOptions<WeatherOptions> options) : IClimateNormals
{
    private static readonly TimeSpan NormalsCacheDuration = TimeSpan.FromHours(24);

    public Task<ClimateNormals> GetAsync(
        ForecastLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken) =>
        // The cached fetch keeps its own timeout, so the caller's token only stops this caller waiting.
        cache.GetOrCreateAsync(
            string.Create(CultureInfo.InvariantCulture, $"normals:{location.Latitude:F3},{location.Longitude:F3}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}"),
            async () =>
            {
                var station = await stations.FindAsync(location.Latitude, location.Longitude, start);
                var normals = await Acis.WithTimeoutAsync(
                    TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds),
                    token => GetNormalsAsync(station, start, end, token));
                return (normals, NormalsCacheDuration);
            }).WaitAsync(cancellationToken);

    private async Task<ClimateNormals> GetNormalsAsync(
        AcisStation station, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["sid"] = station.Id,
            ["sdate"] = Acis.Date(start),
            ["edate"] = Acis.Date(end),
            ["elems"] = new JsonArray(Acis.NormalElement("maxt"), Acis.NormalElement("mint")),
        };

        using var document = await Acis.PostAsync(httpClientFactory, "StnData", body, cancellationToken);
        var highs = new Dictionary<DateOnly, int>();
        var lows = new Dictionary<DateOnly, int>();
        foreach (var day in document.RootElement.GetProperty("data").EnumerateArray())
        {
            var date = DateOnly.ParseExact(day[0].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (int.TryParse(day[1].GetString(), CultureInfo.InvariantCulture, out var high))
            {
                highs[date] = high;
            }

            if (int.TryParse(day[2].GetString(), CultureInfo.InvariantCulture, out var low))
            {
                lows[date] = low;
            }
        }

        return new ClimateNormals(station.Name, highs, lows);
    }
}
