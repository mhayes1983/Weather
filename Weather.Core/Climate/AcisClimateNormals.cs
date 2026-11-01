using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Weather.Core.Models;

namespace Weather.Core.Climate;

/// <summary>Daily normal highs from the nearest ACIS station that has them.</summary>
public sealed class AcisClimateNormals(IHttpClientFactory httpClientFactory, WeatherCache cache) : IClimateNormals
{
    private const string BaseUrl = "https://data.rcc-acis.org";
    private const string MissingValue = "M";

    private static readonly double[] SearchRadiiDegrees = [0.3, 0.75, 1.5, 3];
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

    private async Task<AcisStation> FindNearestStationAsync(
        ForecastLocation location, DateOnly date, CancellationToken cancellationToken)
    {
        foreach (var radius in SearchRadiiDegrees)
        {
            var body = new JsonObject
            {
                ["bbox"] = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{location.Longitude - radius},{location.Latitude - radius},{location.Longitude + radius},{location.Latitude + radius}"),
                ["sdate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["edate"] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["elems"] = NormalHighElements(),
                ["meta"] = "name,sids,ll",
            };

            using var document = await PostAsync("MultiStnData", body, cancellationToken);
            var nearest = document.RootElement.GetProperty("data").EnumerateArray()
                .Where(HasNormal)
                .Select(ToStation)
                .MinBy(station => DistanceSquared(location, station));

            if (nearest is not null)
            {
                return nearest;
            }
        }

        throw new InvalidOperationException($"No climate station found near {location.Zip}.");
    }

    private async Task<ClimateNormals> GetNormalsAsync(
        AcisStation station, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["sid"] = station.Id,
            ["sdate"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["edate"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["elems"] = NormalHighElements(),
        };

        using var document = await PostAsync("StnData", body, cancellationToken);
        var highs = new Dictionary<DateOnly, int>();
        foreach (var day in document.RootElement.GetProperty("data").EnumerateArray())
        {
            if (int.TryParse(day[1].GetString(), CultureInfo.InvariantCulture, out var high))
            {
                highs[DateOnly.ParseExact(day[0].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)] = high;
            }
        }

        return new ClimateNormals(station.Name, highs);
    }

    // A node can only belong to one parent, so each request builds its own.
    private static JsonArray NormalHighElements() => [new JsonObject { ["name"] = "maxt", ["normal"] = "1" }];

    // JsonObject needs no reflection, which trimming on Android would otherwise break.
    private async Task<JsonDocument> PostAsync(string endpoint, JsonObject body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{BaseUrl}/{endpoint}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static bool HasNormal(JsonElement station) =>
        station.GetProperty("data")[0].GetString() is { Length: > 0 } value && value != MissingValue
        && station.GetProperty("meta").TryGetProperty("ll", out _);

    private static AcisStation ToStation(JsonElement station)
    {
        var meta = station.GetProperty("meta");
        var coordinates = meta.GetProperty("ll");
        return new AcisStation(
            meta.GetProperty("sids")[0].GetString()!,
            meta.GetProperty("name").GetString()!,
            coordinates[1].GetDouble(),
            coordinates[0].GetDouble());
    }

    private static double DistanceSquared(ForecastLocation location, AcisStation station)
    {
        var latitudeDelta = station.Latitude - location.Latitude;
        var longitudeDelta = (station.Longitude - location.Longitude) * Math.Cos(location.Latitude * Math.PI / 180);
        return (latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta);
    }

    private sealed record AcisStation(string Id, string Name, double Latitude, double Longitude);
}
