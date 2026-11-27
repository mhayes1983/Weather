using System.Net;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public enum KeyCheckResult
{
    Valid,
    Invalid,
    CouldNotCheck,
}

public sealed record AmbientDeviceSummary(string Id, string? Name);

/// <summary>Tries keys someone has just typed, before they're saved. Each check is one call.</summary>
public sealed class KeyCheck(IHttpClientFactory httpClientFactory, IOptions<WeatherOptions> options)
{
    private const string WeatherUndergroundUrl = "https://api.weather.com/v3/location/near";
    private const string AmbientWeatherUrl = "https://rt.ambientweather.net/v1/devices";
    private const string WeatherApiUrl = "https://api.weatherapi.com/v1/current.json";

    public Task<KeyCheckResult> CheckWeatherUndergroundAsync(string apiKey) => CheckAsync(
        $"{WeatherUndergroundUrl}?postalKey={options.Value.DefaultLocation.Zip}:US&product=pws&format=json&apiKey={Uri.EscapeDataString(apiKey)}");

    public Task<KeyCheckResult> CheckAmbientWeatherAsync(string apiKey, string applicationKey) => CheckAsync(
        $"{AmbientWeatherUrl}?apiKey={Uri.EscapeDataString(apiKey)}&applicationKey={Uri.EscapeDataString(applicationKey)}");

    /// <summary>
    /// The devices on an Ambient Weather account, by the IDs <see cref="AmbientCatalog"/> gives them, so
    /// setup can name them. Never their MAC addresses. Null when the keys don't work or can't be checked.
    /// </summary>
    public async Task<IReadOnlyList<AmbientDeviceSummary>?> ListAmbientDevicesAsync(string apiKey, string applicationKey)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            var account = new AmbientAccount(apiKey.Trim(), applicationKey.Trim(), httpClientFactory, options, TimeProvider.System);
            var devices = await account.GetDevicesAsync(timeout.Token);
            return devices.Select(device => new AmbientDeviceSummary(device.DeviceId, device.Name)).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    public Task<KeyCheckResult> CheckWeatherApiAsync(string apiKey) => CheckAsync(
        $"{WeatherApiUrl}?key={Uri.EscapeDataString(apiKey)}&q={options.Value.DefaultLocation.Zip}&aqi=no");

    private async Task<KeyCheckResult> CheckAsync(string url)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
            using var response = await client.GetAsync(url, timeout.Token);

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => KeyCheckResult.Invalid,
                _ when response.IsSuccessStatusCode => KeyCheckResult.Valid,
                _ => KeyCheckResult.CouldNotCheck,
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return KeyCheckResult.CouldNotCheck;
        }
    }
}
