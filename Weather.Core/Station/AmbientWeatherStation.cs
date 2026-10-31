using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class AmbientWeatherStation(
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IWeatherStation
{
    public const string HomeId = "home";

    private const string DevicesUrl = "https://rt.ambientweather.net/v1/devices";
    private const int HistoryPoints = 288;
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromHours(24);

    // Ambient allows one request a second per key, so calls run one at a time.
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;
    private string? _macAddress;

    private AmbientWeatherOptions Settings => options.Value.AmbientWeather;

    public string Id => HomeId;

    public string Name => Settings.Name;

    public string Source => "AmbientWeather.net";

    public StationHistoryRange HistoryRange => StationHistoryRange.Last24Hours;

    public TimeSpan CurrentCacheDuration => TimeSpan.FromSeconds(Settings.CurrentCacheSeconds);

    public TimeSpan HistoryCacheDuration => TimeSpan.FromMinutes(Settings.HistoryCacheMinutes);

    public TimeSpan OfflineAfter => TimeSpan.FromMinutes(Settings.OfflineAfterMinutes);

    public bool HasKeys =>
        !string.IsNullOrWhiteSpace(Settings.ApiKey) && !string.IsNullOrWhiteSpace(Settings.ApplicationKey);

    public async Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await GetAsync(DevicesUrl, null, cancellationToken));
        var device = SelectDevice(document.RootElement);
        _macAddress = device.GetProperty("macAddress").GetString();

        var data = device.GetProperty("lastData");
        return new StationReading
        {
            StationName = device.TryGetProperty("info", out var info) && info.TryGetProperty("name", out var name)
                ? name.GetString()
                : null,
            ObservedAt = ObservedAt(data),
            TemperatureF = Number(data, "tempf"),
            FeelsLikeF = Number(data, "feelsLike"),
            DewPointF = Number(data, "dewPoint"),
            HumidityPercent = Number(data, "humidity"),
            IndoorTemperatureF = Number(data, "tempinf"),
            IndoorHumidityPercent = Number(data, "humidityin"),
            WindSpeedMph = Number(data, "windspeedmph"),
            WindGustMph = Number(data, "windgustmph"),
            WindDirectionDegrees = Number(data, "winddir"),
            MaxDailyGustMph = Number(data, "maxdailygust"),
            PressureInHg = Number(data, "baromrelin"),
            RainRateInPerHour = Number(data, "hourlyrainin"),
            EventRainIn = Number(data, "eventrainin"),
            DailyRainIn = Number(data, "dailyrainin"),
            WeeklyRainIn = Number(data, "weeklyrainin"),
            MonthlyRainIn = Number(data, "monthlyrainin"),
            YearlyRainIn = Number(data, "yearlyrainin"),
            LastRain = data.TryGetProperty("lastRain", out var lastRain) && lastRain.TryGetDateTimeOffset(out var rainTime)
                ? rainTime
                : null,
            UvIndex = Number(data, "uv"),
            SolarRadiationWm2 = Number(data, "solarradiation"),
            BatteryLow = Number(data, "battout") is double battery ? battery == 0 : null,
        };
    }

    public async Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var macAddress = string.IsNullOrWhiteSpace(Settings.MacAddress) ? _macAddress : Settings.MacAddress;
        if (macAddress is null)
        {
            await GetCurrentAsync(cancellationToken);
            macAddress = _macAddress!;
        }

        using var document = JsonDocument.Parse(await GetAsync(
            $"{DevicesUrl}/{Uri.EscapeDataString(macAddress)}",
            $"&limit={HistoryPoints}",
            cancellationToken));

        var since = timeProvider.GetUtcNow() - HistoryWindow;
        return document.RootElement.EnumerateArray()
            .Select(data => new StationHistoryPoint(
                ObservedAt(data),
                Number(data, "tempf"),
                Number(data, "windspeedmph"),
                Number(data, "windgustmph"),
                Number(data, "baromrelin"),
                Number(data, "hourlyrainin")))
            .Where(point => point.Time >= since)
            .OrderBy(point => point.Time)
            .ToList();
    }

    private async Task<string> GetAsync(string url, string? query, CancellationToken cancellationToken)
    {
        var settings = Settings;
        var fullUrl = $"{url}?apiKey={Uri.EscapeDataString(settings.ApiKey!)}&applicationKey={Uri.EscapeDataString(settings.ApplicationKey!)}{query}";

        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var wait = _nextRequestAt - timeProvider.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken);
            }

            try
            {
                var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
                return await client.GetStringAsync(fullUrl, cancellationToken);
            }
            finally
            {
                _nextRequestAt = timeProvider.GetUtcNow().AddMilliseconds(settings.RequestSpacingMilliseconds);
            }
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private JsonElement SelectDevice(JsonElement devices)
    {
        var macAddress = Settings.MacAddress;
        foreach (var device in devices.EnumerateArray())
        {
            if (string.IsNullOrWhiteSpace(macAddress)
                || string.Equals(device.GetProperty("macAddress").GetString(), macAddress, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        throw new InvalidOperationException("No matching station on the Ambient Weather account.");
    }

    private static DateTimeOffset ObservedAt(JsonElement data) =>
        DateTimeOffset.FromUnixTimeMilliseconds(data.GetProperty("dateutc").GetInt64());

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
