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
        var weatherApi = await store.GetKeyAsync(SettingsStore.WeatherApiKey);

        // Only accounts whose keys are still readable, so a lost key can be entered again.
        var ambientAccounts = new List<SetupAmbientAccount>();
        foreach (var account in SettingsStore.AmbientAccounts(settings))
        {
            if (await store.GetAmbientKeysAsync(account) is not null)
            {
                ambientAccounts.Add(new SetupAmbientAccount(account));
            }
        }

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
                ambientAccounts.Count > 0,
                !string.IsNullOrWhiteSpace(weatherApi),
                stations,
                AppInfo.Current.VersionString,
                ambientAccounts,
                settings?.AmbientStations ?? []),
            AppJson.Default.SetupState);
    }

    /// <summary>
    /// The devices on the accounts in setup, by ID and their name on Ambient Weather, never their MAC
    /// addresses. Typed keys are used where given, else the saved ones. Null if any account can't be read.
    /// </summary>
    public async Task<string> ListAmbientStations(string json)
    {
        var accounts = JsonSerializer.Deserialize(json, AppJson.Default.ListSetupAmbientKeys)!;
        var devices = new List<AmbientDeviceChoice>();
        foreach (var account in accounts.Where(account => !account.Remove))
        {
            var keys = !string.IsNullOrWhiteSpace(account.ApiKey) && !string.IsNullOrWhiteSpace(account.ApplicationKey)
                ? (account.ApiKey, account.ApplicationKey)
                : account.Id is null ? null : await store.GetAmbientKeysAsync(account.Id);
            if (keys is not { } found)
            {
                continue;
            }

            if (await host.Get<KeyCheck>().ListAmbientDevicesAsync(found.ApiKey, found.ApplicationKey) is not { } listed)
            {
                return "null";
            }

            devices.AddRange(listed.Select(device => new AmbientDeviceChoice(device.Id, device.Name)));
        }

        return JsonSerializer.Serialize(devices.DistinctBy(device => device.Id).ToList(), AppJson.Default.ListAmbientDeviceChoice);
    }

    public async Task<string> CheckWeatherUnderground(string apiKey) =>
        (await host.Get<KeyCheck>().CheckWeatherUndergroundAsync(apiKey.Trim())).ToString();

    public async Task<string> CheckAmbientWeather(string apiKey, string applicationKey) =>
        (await host.Get<KeyCheck>().CheckAmbientWeatherAsync(apiKey.Trim(), applicationKey.Trim())).ToString();

    public async Task<string> CheckWeatherApi(string apiKey) =>
        (await host.Get<KeyCheck>().CheckWeatherApiAsync(apiKey.Trim())).ToString();

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

        if (request.AmbientAccounts.Any(account => !account.Remove
            && string.IsNullOrWhiteSpace(account.ApiKey) != string.IsNullOrWhiteSpace(account.ApplicationKey)))
        {
            return new SetupResult(false, "Ambient Weather needs both keys. Enter the other key, or clear both.");
        }

        if (request.AmbientStations.Any(station => !string.IsNullOrWhiteSpace(station.Zip) && !LocationResolver.IsValidZip(station.Zip.Trim())))
        {
            return new SetupResult(false, "Enter a 5-digit ZIP code for each Ambient Weather station, or leave it blank.");
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
        await store.SetKeyAsync(SettingsStore.WeatherApiKey, request.WeatherApiKey);
        var ambientAccounts = await SaveAmbientAccountsAsync(request.AmbientAccounts);

        var (defaults, added) = StationSelection.Split(request.StationIds, host.DefaultStations.Select(station => station.Id));
        var ambientStations = request.AmbientStations
            .Where(station => AmbientCatalog.IsDeviceId(station.DeviceId))
            .DistinctBy(station => station.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(station => station with
            {
                Name = string.IsNullOrWhiteSpace(station.Name) ? null : station.Name.Trim(),
                Zip = string.IsNullOrWhiteSpace(station.Zip) ? null : station.Zip.Trim(),
            })
            .ToList();

        store.Save(new AppSettings(location, defaults, added, ambientAccounts, ambientStations));
        await host.LoadAsync();

        return new SetupResult(true, null);
    }

    // Returns the extra accounts to keep. The primary account keeps its old key names, so updating the app
    // doesn't ask for them again. A removed account's keys are deleted from secure storage.
    private async Task<List<string>> SaveAmbientAccountsAsync(List<SetupAmbientKeys> accounts)
    {
        var known = SettingsStore.AmbientAccounts(store.Load());
        var kept = new List<string>();
        foreach (var account in accounts)
        {
            // Setup always sends the primary account, even before it has keys. A new one has no ID.
            var id = account.Id is not null && (account.Id == SettingsStore.PrimaryAmbientAccount || known.Contains(account.Id))
                ? account.Id
                : Guid.NewGuid().ToString("N");

            if (account.Remove)
            {
                await store.SetKeyAsync(SettingsStore.AmbientApiKeyFor(id), "");
                await store.SetKeyAsync(SettingsStore.AmbientApplicationKeyFor(id), "");
                continue;
            }

            await store.SetKeyAsync(SettingsStore.AmbientApiKeyFor(id), account.ApiKey);
            await store.SetKeyAsync(SettingsStore.AmbientApplicationKeyFor(id), account.ApplicationKey);
            if (id != SettingsStore.PrimaryAmbientAccount && await store.GetAmbientKeysAsync(id) is not null)
            {
                kept.Add(id);
            }
        }

        // Accounts setup didn't send keep their keys.
        kept.AddRange(known.Where(id => id != SettingsStore.PrimaryAmbientAccount
            && !accounts.Any(account => account.Id == id) && !kept.Contains(id)));
        return kept;
    }

    private static Dictionary<string, string?> ParseQuery(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                string? (pair) => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "");
}
