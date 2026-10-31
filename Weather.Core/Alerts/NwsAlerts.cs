using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;

namespace Weather.Core.Alerts;

public sealed class NwsAlerts(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
{
    private const string Endpoint = "https://api.weather.gov/alerts/active";
    private static readonly string[] SeverityOrder = ["Extreme", "Severe", "Moderate", "Minor"];

    public async Task<IReadOnlyList<WeatherAlert>> GetAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        // NWS redirects requests with more than four decimal places.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Endpoint}?point={location.Latitude:0.####},{location.Longitude:0.####}&status=actual&message_type=alert,update");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var now = timeProvider.GetUtcNow();
        return document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties"))
            .Select(ReadAlert)
            .Where(alert => alert.Ends is null || DateTimeOffset.Parse(alert.Ends, CultureInfo.InvariantCulture) > now)
            .OrderBy(alert => SeverityRank(alert.Severity))
            .ThenBy(alert => alert.Ends is null ? DateTimeOffset.MaxValue : DateTimeOffset.Parse(alert.Ends, CultureInfo.InvariantCulture))
            .ToList();
    }

    private static WeatherAlert ReadAlert(JsonElement properties) => new(
        properties.GetProperty("id").GetString()!,
        properties.GetProperty("event").GetString()!,
        Text(properties, "severity") ?? "Unknown",
        Text(properties, "headline"),
        Text(properties, "description"),
        Text(properties, "instruction"),
        Text(properties, "onset") ?? Text(properties, "effective"),
        Text(properties, "ends"));

    private static int SeverityRank(string severity)
    {
        var rank = Array.IndexOf(SeverityOrder, severity);
        return rank < 0 ? SeverityOrder.Length : rank;
    }

    private static string? Text(JsonElement properties, string name) =>
        properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
