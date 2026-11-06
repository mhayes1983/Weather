using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Weather.Core.Models;

namespace Weather.Core.Climate;

/// <summary>Daily normal highs and lows from the nearest ACIS station that has them.</summary>
public sealed class AcisClimateNormals(IHttpClientFactory httpClientFactory, WeatherCache cache) : IClimateNormals
{
    private static readonly TimeSpan NormalsCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan StationCacheDuration = TimeSpan.FromDays(30);

    public async Task<ClimateNormals> GetAsync(
        ForecastLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            $"normals:{location.Zip}:{start:yyyy-MM-dd}",
            async () =>
            {
                var station = await cache.GetOrCreateAsync(
                    $"station:{location.Zip}",
                    async () => (await FindNearestStationAsync(location, start, cancellationToken), StationCacheDuration));
                return (await GetNormalsAsync(station, start, end, cancellationToken), NormalsCacheDuration);
            });
    }

    private Task<AcisStation> FindNearestStationAsync(
        ForecastLocation location, DateOnly date, CancellationToken cancellationToken) =>
        Acis.FindNearestAsync(
            httpClientFactory,
            location.Latitude,
            location.Longitude,
            bbox => new JsonObject
            {
                ["bbox"] = bbox,
                ["sdate"] = Acis.Date(date),
                ["edate"] = Acis.Date(date),
                ["elems"] = new JsonArray(NormalElement("maxt"), NormalElement("mint")),
                ["meta"] = "name,sids,ll",
            },
            HasNormal,
            cancellationToken);

    private async Task<ClimateNormals> GetNormalsAsync(
        AcisStation station, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["sid"] = station.Id,
            ["sdate"] = Acis.Date(start),
            ["edate"] = Acis.Date(end),
            ["elems"] = new JsonArray(NormalElement("maxt"), NormalElement("mint")),
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

    // A node can only belong to one parent, so each request builds its own.
    internal static JsonObject NormalElement(string name) => new() { ["name"] = name, ["normal"] = "1" };

    // Both normals are needed, so a station with only highs doesn't leave the lows unhighlighted.
    private static bool HasNormal(JsonElement station) =>
        Acis.IsPresent(station.GetProperty("data")[0]) && Acis.IsPresent(station.GetProperty("data")[1]);
}
