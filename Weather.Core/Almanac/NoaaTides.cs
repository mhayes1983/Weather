using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;

namespace Weather.Core.Almanac;

/// <summary>A NOAA tide prediction station. Its name is public, so the page can credit it.</summary>
public sealed record TideStation(string Id, string Name);

/// <summary>
/// High and low tide predictions from NOAA CO-OPS (Tides and Currents), which needs no key. The station
/// list holds about 3,500 prediction stations, harmonic and subordinate, and both give highs and lows.
/// </summary>
public sealed class NoaaTides(IHttpClientFactory httpClientFactory)
{
    private const string StationsEndpoint = "https://api.tidesandcurrents.noaa.gov/mdapi/prod/webapi/stations.json?type=tidepredictions";
    private const string PredictionsEndpoint = "https://api.tidesandcurrents.noaa.gov/api/prod/datagetter";

    /// <summary>
    /// Further than this, a station's tides could be well off in time and height, or belong to another body of
    /// water, so inland places show none. Topsham's nearest, on the Androscoggin in Brunswick, is under a mile.
    /// </summary>
    public const double MaxStationMiles = 10;

    /// <summary>The nearest prediction station within <see cref="MaxStationMiles"/>, or null.</summary>
    public async Task<TideStation?> FindStationAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(StationsEndpoint, cancellationToken));

        TideStation? nearest = null;
        var nearestMiles = MaxStationMiles;
        foreach (var station in document.RootElement.GetProperty("stations").EnumerateArray())
        {
            if (Number(station, "lat") is not double latitude
                || Number(station, "lng") is not double longitude
                || Text(station, "id") is not { Length: > 0 } id)
            {
                continue;
            }

            var miles = GeoDistance.Miles(location.Latitude, location.Longitude, latitude, longitude);
            if (miles <= nearestMiles)
            {
                nearest = new TideStation(id, Text(station, "name") ?? id);
                nearestMiles = miles;
            }
        }

        return nearest;
    }

    /// <summary>Each date's highs and lows in the station's local time, from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<AlmanacTide>>> GetAsync(
        string stationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        // lst_ldt is the station's own local time, with daylight saving. MLLW is the datum charts use.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{PredictionsEndpoint}?product=predictions&application=Weather&datum=MLLW&station={Uri.EscapeDataString(stationId)}&begin_date={from:yyyyMMdd}&end_date={to:yyyyMMdd}&interval=hilo&units=english&time_zone=lst_ldt&format=json");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        var root = document.RootElement;

        // Errors come back as 200 with an error message instead of predictions.
        if (!root.TryGetProperty("predictions", out var predictions))
        {
            throw new InvalidOperationException("NOAA returned no tide predictions.");
        }

        var tides = new Dictionary<DateOnly, List<AlmanacTide>>();
        foreach (var prediction in predictions.EnumerateArray())
        {
            // "2026-11-25 13:20"
            var type = Text(prediction, "type");
            if (!DateTime.TryParseExact(Text(prediction, "t"), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                || !double.TryParse(Text(prediction, "v"), NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
                || type is not ("H" or "L"))
            {
                continue;
            }

            var date = DateOnly.FromDateTime(time);
            if (!tides.TryGetValue(date, out var list))
            {
                tides[date] = list = [];
            }

            list.Add(new AlmanacTide(time.ToString("HH:mm", CultureInfo.InvariantCulture), type == "H", Math.Round(height, 1)));
        }

        return tides.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<AlmanacTide>)pair.Value);
    }

    private static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
