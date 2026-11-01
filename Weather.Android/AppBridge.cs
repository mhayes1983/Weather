using System.Globalization;
using System.Text.Json;
using Weather.Core.Api;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Mobile;

/// <summary>
/// What the React client can call. Values cross as strings, and keys only ever go one way:
/// the client is told whether a key is saved, never what it is.
/// </summary>
public sealed class AppBridge(AppHost host, SettingsStore store)
{
    /// <summary>Returns the status code, a newline, then the JSON body.</summary>
    public async Task<string> Api(string url)
    {
        var separator = url.IndexOf('?');
        var path = separator < 0 ? url : url[..separator];
        var query = separator < 0 ? "" : url[(separator + 1)..];

        var response = await host.Get<LocalApi>().HandleAsync(path, ParseQuery(query));
        return string.Create(CultureInfo.InvariantCulture, $"{response.StatusCode}\n{response.Json}");
    }

    public async Task<string> GetSetupState()
    {
        var settings = store.Load();
        var weatherUnderground = await store.GetKeyAsync(SettingsStore.WeatherUndergroundKey);
        var ambientApiKey = await store.GetKeyAsync(SettingsStore.AmbientApiKey);
        var ambientApplicationKey = await store.GetKeyAsync(SettingsStore.AmbientApplicationKey);

        var stations = host.DefaultStations
            .Select(station => new SetupStation(
                station.Id,
                station.Name ?? station.Id,
                settings?.StationIds.Contains(station.Id) ?? true))
            .Concat((settings?.AddedStationIds ?? []).Select(id => new SetupStation(id, id, true)))
            .ToList();

        return JsonSerializer.Serialize(
            new SetupState(
                settings is not null,
                settings?.Location.Zip,
                !string.IsNullOrWhiteSpace(weatherUnderground),
                !string.IsNullOrWhiteSpace(ambientApiKey) && !string.IsNullOrWhiteSpace(ambientApplicationKey),
                stations),
            AppJson.Default.SetupState);
    }

    public async Task<string> CheckWeatherUnderground(string apiKey) =>
        (await host.Get<KeyCheck>().CheckWeatherUndergroundAsync(apiKey.Trim())).ToString();

    public async Task<string> CheckAmbientWeather(string apiKey, string applicationKey) =>
        (await host.Get<KeyCheck>().CheckAmbientWeatherAsync(apiKey.Trim(), applicationKey.Trim())).ToString();

    public async Task OpenLink(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }

    public async Task<string> SaveSetup(string json)
    {
        var result = await SaveAsync(JsonSerializer.Deserialize(json, AppJson.Default.SetupRequest)!);
        return JsonSerializer.Serialize(result, AppJson.Default.SetupResult);
    }

    private async Task<SetupResult> SaveAsync(SetupRequest request)
    {
        var zip = request.Zip.Trim();
        if (!LocationResolver.IsValidZip(zip))
        {
            return new SetupResult(false, "Enter a 5-digit ZIP code.");
        }

        if (string.IsNullOrWhiteSpace(request.AmbientApiKey) != string.IsNullOrWhiteSpace(request.AmbientApplicationKey))
        {
            return new SetupResult(false, "Ambient Weather needs both keys. Enter the other key, or clear both.");
        }

        ForecastLocation location;
        try
        {
            location = await host.Get<LocationResolver>().ResolveAsync(zip);
        }
        catch (LocationNotFoundException)
        {
            return new SetupResult(false, "That ZIP code wasn't found.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new SetupResult(false, "The ZIP code couldn't be checked. Check your connection and try again.");
        }

        await store.SetKeyAsync(SettingsStore.WeatherUndergroundKey, request.WeatherUndergroundKey);
        await store.SetKeyAsync(SettingsStore.AmbientApiKey, request.AmbientApiKey);
        await store.SetKeyAsync(SettingsStore.AmbientApplicationKey, request.AmbientApplicationKey);

        var (defaults, added) = StationSelection.Split(request.StationIds, host.DefaultStations.Select(station => station.Id));

        store.Save(new AppSettings(location, defaults, added));
        await host.LoadAsync();

        return new SetupResult(true, null);
    }

    private static Dictionary<string, string?> ParseQuery(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                string? (pair) => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "");
}
