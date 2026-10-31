using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class WeatherUndergroundStation(
    WeatherStationOptions station,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options) : IWeatherStation
{
    private const string BaseUrl = "https://api.weather.com/v2/pws/observations";
    private const double WindChillBelowF = 50;
    private const double HeatIndexAboveF = 68;

    public string Id => station.Id;

    public string Name => string.IsNullOrWhiteSpace(station.Name) ? station.Id : station.Name;

    public string Source => "Weather Underground";

    public StationHistoryRange HistoryRange => StationHistoryRange.Today;

    public TimeSpan CurrentCacheDuration => TimeSpan.FromMinutes(options.Value.WeatherUndergroundStations.CurrentCacheMinutes);

    public TimeSpan HistoryCacheDuration => TimeSpan.FromMinutes(options.Value.WeatherUndergroundStations.HistoryCacheMinutes);

    public bool HasKeys => !string.IsNullOrWhiteSpace(options.Value.WeatherUnderground.ApiKey);

    public async Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // Stations with nothing from the past hour return an empty body.
        var body = await GetAsync("current", cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new StationNoDataException();
        }

        using var document = JsonDocument.Parse(body);
        var observations = document.RootElement.GetProperty("observations");
        if (observations.GetArrayLength() == 0)
        {
            throw new StationNoDataException();
        }

        var observation = observations[0];
        var imperial = observation.GetProperty("imperial");
        var temperature = Number(imperial, "temp");

        return new StationReading
        {
            StationName = Name,
            ObservedAt = observation.GetProperty("obsTimeUtc").GetDateTimeOffset(),
            TemperatureF = temperature,
            FeelsLikeF = temperature switch
            {
                < WindChillBelowF => Number(imperial, "windChill") ?? temperature,
                > HeatIndexAboveF => Number(imperial, "heatIndex") ?? temperature,
                _ => temperature,
            },
            DewPointF = Number(imperial, "dewpt"),
            HumidityPercent = Number(observation, "humidity"),
            WindSpeedMph = Number(imperial, "windSpeed"),
            WindGustMph = Number(imperial, "windGust"),
            WindDirectionDegrees = Number(observation, "winddir"),
            PressureInHg = Number(imperial, "pressure"),
            RainRateInPerHour = Number(imperial, "precipRate"),
            DailyRainIn = Number(imperial, "precipTotal"),
            UvIndex = Number(observation, "uv"),
            SolarRadiationWm2 = Number(observation, "solarRadiation"),
        };
    }

    public async Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var body = await GetAsync("all/1day", cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("observations").EnumerateArray()
            .Select(observation =>
            {
                var imperial = observation.GetProperty("imperial");
                return new StationHistoryPoint(
                    observation.GetProperty("obsTimeUtc").GetDateTimeOffset(),
                    Number(imperial, "tempAvg"),
                    Number(imperial, "windspeedAvg"),
                    Number(imperial, "windgustHigh"),
                    (Number(imperial, "pressureMax") + Number(imperial, "pressureMin")) / 2,
                    Number(imperial, "precipRate"));
            })
            .OrderBy(point => point.Time)
            .ToList();
    }

    private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/{path}?stationId={Uri.EscapeDataString(station.Id)}&format=json&units=e&apiKey={Uri.EscapeDataString(options.Value.WeatherUnderground.ApiKey!)}";

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        return await client.GetStringAsync(url, cancellationToken);
    }

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
