using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.DailyForecast;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Station;

/// <summary>
/// Stations measure, but they can't say whether it's cloudy, so the description comes from
/// Open-Meteo's model for the station's location. A failure only hides it, because the readings stand alone.
/// </summary>
public sealed class StationSky(
    StationLocator locator,
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    ILogger<StationSky> logger)
{
    public Task<CurrentSky?> GetAsync(IWeatherStation station) =>
        cache.GetOrCreateAsync<CurrentSky?>(
            $"station-sky:{station.Id}",
            async () =>
            {
                // The result is shared through the cache, so this has its own timeout.
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var location = await locator.GetAsync(station, timeout.Token);
                    var sky = await FetchAsync(location, timeout.Token);
                    return (sky, TimeSpan.FromMinutes(options.Value.OpenMeteoStations.CurrentCacheMinutes));
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Sky for station {Id} failed: {Error}", station.Id, exception.GetType().Name);
                    return (null, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private async Task<CurrentSky?> FetchAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{OpenMeteoSource.ForecastEndpoint}?latitude={location.Latitude}&longitude={location.Longitude}&current=weather_code,is_day");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var current = document.RootElement.GetProperty("current");
        var code = current.GetProperty("weather_code");
        if (code.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var (conditions, icon) = WeatherConditions.Describe(code.GetInt32(), current.GetProperty("is_day").GetInt32() != 0);
        return new CurrentSky(conditions, icon);
    }
}
