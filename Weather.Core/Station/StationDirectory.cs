using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Station;

/// <summary>
/// Finds the configured stations, the Ambient Weather devices on the configured accounts, and any other
/// Weather Underground station or Open-Meteo ZIP a browser has looked up. Those aren't known in advance,
/// so a well-formed ID is enough.
/// </summary>
public sealed partial class StationDirectory(
    IEnumerable<IWeatherStation> stations,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    LocationResolver locations,
    TimeProvider timeProvider,
    AmbientCatalog? ambient = null)
{
    // Registration order, with the home station first.
    private readonly List<StationSummary> _configured =
        stations.Select(station => new StationSummary(station.Id, station.Name, station.Zip)).ToList();

    /// <summary>The configured stations, with any other Ambient Weather devices after home.</summary>
    /// <exception cref="StationListUnavailableException">An Ambient Weather account can't be read and has no earlier list.</exception>
    public async Task<IReadOnlyList<StationSummary>> GetStationsAsync(CancellationToken cancellationToken = default)
    {
        var list = ambient is null ? new AmbientStationList([], true) : await ambient.ListStationsAsync(cancellationToken);
        if (!list.Complete)
        {
            throw new StationListUnavailableException("Ambient Weather couldn't be reached, so the station list is incomplete. Try again in a few minutes.");
        }

        var devices = list.Stations;
        if (devices.Count == 0)
        {
            return _configured;
        }

        var home = _configured.FindIndex(station => station.Id == AmbientWeatherStation.HomeId);
        return
        [
            .. _configured.Take(home + 1),
            .. devices.Select(station => new StationSummary(station.Id, station.Name, station.Zip)),
            .. _configured.Skip(home + 1),
        ];
    }

    /// <summary>
    /// Like <see cref="Find"/>, and also finds Ambient Weather devices other than home, which needs the
    /// accounts asked. Returns null when the ID is badly formed or no device has it.
    /// </summary>
    public async Task<IWeatherStation?> FindAsync(string? id, CancellationToken cancellationToken = default) =>
        AmbientCatalog.IsDeviceId(id)
            ? ambient is null ? null : await ambient.FindAsync(id!, cancellationToken)
            : Find(id);

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
