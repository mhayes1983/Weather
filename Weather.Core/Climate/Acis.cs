using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Weather.Core.Climate;

internal sealed record AcisStation(string Id, string Name, double Latitude, double Longitude);

/// <summary>Request and parsing helpers shared by the ACIS normals and almanac.</summary>
internal static class Acis
{
    private const string BaseUrl = "https://data.rcc-acis.org";
    private const string MissingValue = "M";

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

    public static DateOnly? ParseDate(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    // A node can only belong to one parent, so each request builds its own.
    public static JsonObject NormalElement(string name) => new() { ["name"] = name, ["normal"] = "1" };

    public static AcisStation ToStation(JsonElement station)
    {
        var meta = station.GetProperty("meta");
        var coordinates = meta.GetProperty("ll");
        return new AcisStation(
            meta.GetProperty("sids")[0].GetString()!,
            meta.GetProperty("name").GetString()!,
            coordinates[1].GetDouble(),
            coordinates[0].GetDouble());
    }

    public static double DistanceSquared(double latitude, double longitude, AcisStation station)
    {
        var latitudeDelta = station.Latitude - latitude;
        var longitudeDelta = (station.Longitude - longitude) * Math.Cos(latitude * Math.PI / 180);
        return (latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta);
    }

    /// <summary>Runs <paramref name="fetch"/> with its own timeout, so a shared cache entry never inherits another caller's.</summary>
    public static async Task<T> WithTimeoutAsync<T>(TimeSpan timeout, Func<CancellationToken, Task<T>> fetch)
    {
        using var source = new CancellationTokenSource(timeout);
        return await fetch(source.Token);
    }
}
