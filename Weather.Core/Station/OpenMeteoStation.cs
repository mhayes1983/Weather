using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Station;

/// <summary>
/// Open-Meteo's model values for a ZIP code. It needs no key and uses no Weather Underground
/// quota, but nothing here was measured, so reports say so.
/// </summary>
public sealed class OpenMeteoStation(
    string id,
    string zip,
    LocationResolver locations,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IWeatherStation
{
    public const string IdPrefix = "om-";

    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";
    private const string Query =
        "&current=temperature_2m,apparent_temperature,dew_point_2m,relative_humidity_2m,wind_speed_10m,wind_gusts_10m,wind_direction_10m,pressure_msl,precipitation,uv_index,shortwave_radiation"
        + "&hourly=temperature_2m,wind_speed_10m,wind_gusts_10m,pressure_msl,precipitation"
        + "&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch&timeformat=unixtime&past_days=1&forecast_days=1";
    private const double InHgPerHpa = 0.02953;
    private static readonly TimeSpan HistoryLength = TimeSpan.FromHours(24);

    public string Id => id;

    public string Name => $"ZIP {zip}";

    public string Source => "Open-Meteo";

    public StationHistoryRange HistoryRange => StationHistoryRange.Last24Hours;

    public TimeSpan CurrentCacheDuration => TimeSpan.FromMinutes(options.Value.OpenMeteoStations.CurrentCacheMinutes);

    public TimeSpan HistoryCacheDuration => TimeSpan.FromMinutes(options.Value.OpenMeteoStations.HistoryCacheMinutes);

    public TimeSpan OfflineAfter => TimeSpan.FromMinutes(options.Value.OpenMeteoStations.OfflineAfterMinutes);

    public bool HasKeys => true;

    public bool CanLocate => true;

    public bool Modelled => true;

    // Hourly values would otherwise count as gaps in the data.
    public int HistoryMaxGapMinutes => 90;

    public async Task<GeoPoint> GetCoordinatesAsync(CancellationToken cancellationToken)
    {
        var location = await locations.ResolveAsync(zip, cancellationToken);
        return new GeoPoint(location.Latitude, location.Longitude);
    }

    public async Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var location = await locations.ResolveAsync(zip, cancellationToken);
        using var document = await GetDocumentAsync(location, cancellationToken);

        var current = document.RootElement.GetProperty("current");
        var interval = current.GetProperty("interval").GetDouble();

        // The day's totals stop at now, because the rest of the hourly data is forecast.
        var now = timeProvider.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var midnight = new DateTimeOffset(localNow.Date, zone.GetUtcOffset(localNow.Date));
        var today = ReadHours(document).Where(hour => hour.Time > midnight && hour.Time <= now).ToList();

        return new StationReading
        {
            StationName = $"{location.Place}, {location.State}",
            ObservedAt = DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64()),
            TemperatureF = Number(current, "temperature_2m"),
            FeelsLikeF = Number(current, "apparent_temperature"),
            DewPointF = Number(current, "dew_point_2m"),
            HumidityPercent = Number(current, "relative_humidity_2m"),
            WindSpeedMph = Number(current, "wind_speed_10m"),
            WindGustMph = Number(current, "wind_gusts_10m"),
            WindDirectionDegrees = Number(current, "wind_direction_10m"),
            MaxDailyGustMph = today.Max(hour => hour.WindGustMph),
            PressureInHg = ToInHg(Number(current, "pressure_msl")),

            // This is the rain over the interval, not an hourly rate.
            RainRateInPerHour = Number(current, "precipitation") * TimeSpan.FromHours(1).TotalSeconds / interval,
            DailyRainIn = today.Sum(hour => hour.RainRateInPerHour),
            UvIndex = Number(current, "uv_index"),
            SolarRadiationWm2 = Number(current, "shortwave_radiation"),
        };
    }

    public async Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var location = await locations.ResolveAsync(zip, cancellationToken);
        using var document = await GetDocumentAsync(location, cancellationToken);

        var now = timeProvider.GetUtcNow();
        return ReadHours(document)
            .Where(point => point.Time > now - HistoryLength && point.Time <= now)
            .ToList();
    }

    private async Task<JsonDocument> GetDocumentAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{Endpoint}?latitude={location.Latitude}&longitude={location.Longitude}{Query}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        return JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
    }

    // Hourly precipitation is the total for the hour before, which is also its rate in in/hr.
    private static IEnumerable<StationHistoryPoint> ReadHours(JsonDocument document)
    {
        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time");

        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            yield return new StationHistoryPoint(
                DateTimeOffset.FromUnixTimeSeconds(times[i].GetInt64()),
                Number(hourly, "temperature_2m", i),
                Number(hourly, "wind_speed_10m", i),
                Number(hourly, "wind_gusts_10m", i),
                ToInHg(Number(hourly, "pressure_msl", i)),
                Number(hourly, "precipitation", i));
        }
    }

    private static double? ToInHg(double? hectopascals) => hectopascals * InHgPerHpa;

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private static double? Number(JsonElement data, string name, int index) =>
        data.TryGetProperty(name, out var values) && index < values.GetArrayLength() && values[index].ValueKind == JsonValueKind.Number
            ? values[index].GetDouble()
            : null;
}
