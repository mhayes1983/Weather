using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Station;

/// <summary>
/// Finds the configured stations, plus any other Weather Underground station or Open-Meteo
/// ZIP a browser has looked up. Those aren't known in advance, so a well-formed ID is enough.
/// </summary>
public sealed partial class StationDirectory(
    IEnumerable<IWeatherStation> stations,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    LocationResolver locations,
    TimeProvider timeProvider)
{
    // Registration order, with the home station first.
    public IReadOnlyList<StationSummary> Stations { get; } =
        stations.Select(station => new StationSummary(station.Id, station.Name)).ToList();

    public static bool IsValidId(string id) => StationIdPattern().IsMatch(id);

    public bool IsConfigured(string id) =>
        stations.Any(station => string.Equals(station.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns null when the ID is badly formed.</summary>
    public IWeatherStation? Find(string? id)
    {
        id ??= AmbientWeatherStation.HomeId;

        var configured = stations.FirstOrDefault(station =>
            string.Equals(station.Id, id, StringComparison.OrdinalIgnoreCase));
        if (configured is not null)
        {
            return configured;
        }

        if (OpenMeteoIdPattern().Match(id) is { Success: true } match)
        {
            return CreateOpenMeteo(match.Groups["zip"].Value);
        }

        return IsValidId(id)
            ? new WeatherUndergroundStation(new WeatherStationOptions { Id = id }, httpClientFactory, options, lookedUp: true)
            : null;
    }

    public OpenMeteoStation CreateOpenMeteo(string zip) =>
        new($"{OpenMeteoStation.IdPrefix}{zip}", zip, locations, httpClientFactory, options, timeProvider);

    [GeneratedRegex("^[A-Za-z0-9]{3,32}$")]
    private static partial Regex StationIdPattern();

    // The hyphen keeps these apart from Weather Underground IDs.
    [GeneratedRegex(@"^om-(?<zip>\d{5})$")]
    private static partial Regex OpenMeteoIdPattern();
}
