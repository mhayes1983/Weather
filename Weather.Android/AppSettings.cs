using System.Text.Json;
using System.Text.Json.Serialization;
using Weather.Core.Models;

namespace Weather.Mobile;

/// <summary>
/// Everything from setup except the keys, which only ever live in secure storage. Settings saved by
/// older versions have no Ambient Weather lists, which read as null.
/// </summary>
public sealed record AppSettings(
    ForecastLocation Location,
    List<string> StationIds,
    List<string> AddedStationIds,
    List<string>? AmbientAccountIds = null,
    List<AmbientStationSetting>? AmbientStations = null);

/// <summary>An Ambient Weather device's settings, by the ID Core gives it. Never its MAC address.</summary>
public sealed record AmbientStationSetting(string DeviceId, string? Name, string? Zip, bool Hidden, bool Home);

public sealed record SetupStation(string Id, string Name, bool Selected);

public sealed record SetupAmbientAccount(string Id);

public sealed record SetupState(
    bool Complete,
    string? Zip,
    bool HasWeatherUnderground,
    bool HasAmbientWeather,
    bool HasWeatherApi,
    List<SetupStation> Stations,
    string AppVersion,
    List<SetupAmbientAccount> AmbientAccounts,
    List<AmbientStationSetting> AmbientStations);

/// <summary>
/// An Ambient Weather account in setup. A null ID is a new account. Null keys leave the saved
/// ones alone, so keys only ever cross the bridge going in.
/// </summary>
public sealed record SetupAmbientKeys(string? Id, string? ApiKey, string? ApplicationKey, bool Remove);

public sealed record AmbientDeviceChoice(string Id, string? Name);

/// <summary>A null key leaves the saved one alone, and a blank key removes it.</summary>
public sealed record SetupRequest(
    string Zip,
    string? WeatherUndergroundKey,
    string? WeatherApiKey,
    List<string> StationIds,
    List<SetupAmbientKeys> AmbientAccounts,
    List<AmbientStationSetting> AmbientStations);

public sealed record SetupResult(bool Ok, string? Error);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SetupState))]
[JsonSerializable(typeof(SetupRequest))]
[JsonSerializable(typeof(SetupResult))]
[JsonSerializable(typeof(List<SetupAmbientKeys>))]
[JsonSerializable(typeof(List<AmbientDeviceChoice>))]
internal sealed partial class AppJson : JsonSerializerContext;

public sealed class SettingsStore
{
    public const string WeatherUndergroundKey = "weather-underground-key";
    public const string AmbientApiKey = "ambient-api-key";
    public const string AmbientApplicationKey = "ambient-application-key";
    public const string WeatherApiKey = "weatherapi-key";

    /// <summary>The first Ambient Weather account, whose keys keep the names they had before there were several.</summary>
    public const string PrimaryAmbientAccount = "primary";

    public static string AmbientApiKeyFor(string account) =>
        account == PrimaryAmbientAccount ? AmbientApiKey : $"{AmbientApiKey}-{account}";

    public static string AmbientApplicationKeyFor(string account) =>
        account == PrimaryAmbientAccount ? AmbientApplicationKey : $"{AmbientApplicationKey}-{account}";

    /// <summary>The primary account first, then the others, whether or not their keys are still readable.</summary>
    public static List<string> AmbientAccounts(AppSettings? settings) =>
        [PrimaryAmbientAccount, .. settings?.AmbientAccountIds ?? []];

    public async Task<(string ApiKey, string ApplicationKey)?> GetAmbientKeysAsync(string account)
    {
        var apiKey = await GetKeyAsync(AmbientApiKeyFor(account));
        var applicationKey = await GetKeyAsync(AmbientApplicationKeyFor(account));
        return string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(applicationKey) ? null : (apiKey, applicationKey);
    }

    private const string SettingsName = "settings";

    public AppSettings? Load() =>
        Preferences.Default.Get<string?>(SettingsName, null) is { } json
            ? JsonSerializer.Deserialize(json, AppJson.Default.AppSettings)
            : null;

    public void Save(AppSettings settings) =>
        Preferences.Default.Set(SettingsName, JsonSerializer.Serialize(settings, AppJson.Default.AppSettings));

    public async Task<string?> GetKeyAsync(string name)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(name);
        }
        catch (Exception)
        {
            // Android can lose the encryption key, for example after a restore. Setup can then ask again.
            return null;
        }
    }

    public async Task SetKeyAsync(string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            SecureStorage.Default.Remove(name);
        }
        else
        {
            await SecureStorage.Default.SetAsync(name, value.Trim());
        }
    }
}
