using System.Text.Json;
using System.Text.Json.Serialization;
using Weather.Core.Models;

namespace Weather.Mobile;

/// <summary>Everything from setup except the keys, which only ever live in secure storage.</summary>
public sealed record AppSettings(ForecastLocation Location, List<string> StationIds, List<string> AddedStationIds);

public sealed record SetupStation(string Id, string Name, bool Selected);

public sealed record SetupState(
    bool Complete,
    string? Zip,
    bool HasWeatherUnderground,
    bool HasAmbientWeather,
    List<SetupStation> Stations);

/// <summary>A null key leaves the saved one alone, and a blank key removes it.</summary>
public sealed record SetupRequest(
    string Zip,
    string? WeatherUndergroundKey,
    string? AmbientApiKey,
    string? AmbientApplicationKey,
    List<string> StationIds);

public sealed record SetupResult(bool Ok, string? Error);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SetupState))]
[JsonSerializable(typeof(SetupRequest))]
[JsonSerializable(typeof(SetupResult))]
internal sealed partial class AppJson : JsonSerializerContext;

public sealed class SettingsStore
{
    public const string WeatherUndergroundKey = "weather-underground-key";
    public const string AmbientApiKey = "ambient-api-key";
    public const string AmbientApplicationKey = "ambient-application-key";

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
