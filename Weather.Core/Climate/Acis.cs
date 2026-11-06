using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weather.Core.Climate;

internal sealed record AcisStation(string Id, string Name, double Latitude, double Longitude);

/// <summary>Calls shared by the ACIS normals and almanac.</summary>
internal static class Acis
{
    private const string BaseUrl = "https://data.rcc-acis.org";
    private const string MissingValue = "M";

    private static readonly double[] SearchRadiiDegrees = [0.3, 0.75, 1.5, 3];

    /// <summary>The nearest station that <paramref name="accept"/> takes, searching wider until one is found.</summary>
    /// <param name="request">Builds the search for a bounding box. A node can only belong to one parent, so each call builds its own.</param>
    public static async Task<AcisStation> FindNearestAsync(
        IHttpClientFactory httpClientFactory,
        double latitude,
        double longitude,
        Func<string, JsonObject> request,
        Func<JsonElement, bool> accept,
        CancellationToken cancellationToken)
    {
        foreach (var radius in SearchRadiiDegrees)
        {
            var bbox = string.Create(
                CultureInfo.InvariantCulture,
                $"{longitude - radius},{latitude - radius},{longitude + radius},{latitude + radius}");

            using var document = await PostAsync(httpClientFactory, "MultiStnData", request(bbox), cancellationToken);
            var nearest = document.RootElement.GetProperty("data").EnumerateArray()
                .Where(station => station.GetProperty("meta").TryGetProperty("ll", out _) && accept(station))
                .Select(ToStation)
                .MinBy(station => DistanceSquared(latitude, longitude, station));

            if (nearest is not null)
            {
                return nearest;
            }
        }

        throw new InvalidOperationException("No climate station found nearby.");
    }

    // JsonObject needs no reflection, which trimming on Android would otherwise break.
    public static async Task<JsonDocument> PostAsync(
        IHttpClientFactory httpClientFactory, string endpoint, JsonObject body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{BaseUrl}/{endpoint}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public static bool IsPresent(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && text != MissingValue;

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    private static double DistanceSquared(double latitude, double longitude, AcisStation station)
    {
        var latitudeDelta = station.Latitude - latitude;
        var longitudeDelta = (station.Longitude - longitude) * Math.Cos(latitude * Math.PI / 180);
        return (latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta);
    }
}
