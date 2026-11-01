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

/// <summary>Tries keys someone has just typed, before they're saved. Each check is one call.</summary>
public sealed class KeyCheck(IHttpClientFactory httpClientFactory, IOptions<WeatherOptions> options)
{
    private const string WeatherUndergroundUrl = "https://api.weather.com/v3/location/near";
    private const string AmbientWeatherUrl = "https://rt.ambientweather.net/v1/devices";

    public Task<KeyCheckResult> CheckWeatherUndergroundAsync(string apiKey) => CheckAsync(
        $"{WeatherUndergroundUrl}?postalKey={options.Value.DefaultLocation.Zip}:US&product=pws&format=json&apiKey={Uri.EscapeDataString(apiKey)}");

    public Task<KeyCheckResult> CheckAmbientWeatherAsync(string apiKey, string applicationKey) => CheckAsync(
        $"{AmbientWeatherUrl}?apiKey={Uri.EscapeDataString(apiKey)}&applicationKey={Uri.EscapeDataString(applicationKey)}");

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
