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

    public string? Zip => string.IsNullOrWhiteSpace(options.Value.DefaultLocation.Zip) ? null : options.Value.DefaultLocation.Zip;

    public string Source => "AmbientWeather.net";

    public StationHistoryRange HistoryRange => StationHistoryRange.Last24Hours;

    public TimeSpan CurrentCacheDuration => TimeSpan.FromSeconds(Settings.CurrentCacheSeconds);

    public TimeSpan HistoryCacheDuration => TimeSpan.FromMinutes(Settings.HistoryCacheMinutes);

    public TimeSpan OfflineAfter => TimeSpan.FromMinutes(Settings.OfflineAfterMinutes);

    public bool HasKeys =>
        !string.IsNullOrWhiteSpace(Settings.ApiKey) && !string.IsNullOrWhiteSpace(Settings.ApplicationKey);

    // Forecasts use the configured location, so Ambient's street address and coordinates are never read.
    public bool CanLocate => true;

    public Task<GeoPoint> GetCoordinatesAsync(CancellationToken cancellationToken)
    {
        var location = options.Value.DefaultLocation;
        return Task.FromResult(new GeoPoint(location.Latitude, location.Longitude));
    }

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
            BatteryLow = BatteryLow(data, "battout"),
            WindSpeedAvg2mMph = Number(data, "windspdmph_avg2m"),
            WindDirectionAvg2mDegrees = Number(data, "winddir_avg2m"),
            WindSpeedAvg10mMph = Number(data, "windspdmph_avg10m"),
            WindDirectionAvg10mDegrees = Number(data, "winddir_avg10m"),
            WindGustDirectionDegrees = Number(data, "windgustdir"),
            Rain24HourIn = Number(data, "24hourrainin"),
            TotalRainIn = Number(data, "totalrainin"),
            ExtraSensors = ExtraSensors(data),
            SoilSensors = SoilSensors(data),
            LeakSensors = LeakSensors(data),
            AirQuality = ReadAirQuality(data),
            Lightning = ReadLightning(data),
            LowBatteries = LowBatteries(data),
        };
    }

    private const int SensorChannels = 10;
    private const int LeakChannels = 4;

    // Other sensors' battery flags, from Ambient's Device Data Specs. Extra temperature sensors carry theirs on
    // each channel. Most are 1 for OK and 0 for low, but the lightning detector and leak sensors are the reverse.
    private static readonly (string Field, string Name, bool OneIsLow)[] Batteries =
    [
        ("battin", "indoor sensor", false),
        ("batt_lightning", "lightning detector", true),
        ("batt_co2", "CO2 sensor", false),
        ("batt_25", "air quality sensor", false),
        ("batt_cellgateway", "cellular gateway", false),
        .. Enumerable.Range(1, LeakChannels).Select(channel => ($"batleak{channel}", $"leak sensor {channel}", true)),
        .. Enumerable.Range(1, 4).Select(channel => ($"battsm{channel}", $"soil sensor {channel}", false)),
    ];

    private static List<ExtraSensor> ExtraSensors(JsonElement data)
    {
        var sensors = new List<ExtraSensor>();
        for (var channel = 1; channel <= SensorChannels; channel++)
        {
            var temperature = Number(data, $"temp{channel}f");
            var humidity = Number(data, $"humidity{channel}");
            if (temperature is not null || humidity is not null)
            {
                sensors.Add(new ExtraSensor(channel, temperature, humidity, BatteryLow(data, $"batt{channel}")));
            }
        }

        return sensors;
    }

    private static List<SoilSensor> SoilSensors(JsonElement data)
    {
        var sensors = new List<SoilSensor>();
        for (var channel = 1; channel <= SensorChannels; channel++)
        {
            var temperature = Number(data, $"soiltemp{channel}f");
            var moisture = Number(data, $"soilhum{channel}");
            if (temperature is not null || moisture is not null)
            {
                sensors.Add(new SoilSensor(channel, temperature, moisture));
            }
        }

        return sensors;
    }

    // Ambient sends 1 for a leak and 0 for dry. Other values mean the sensor isn't reporting.
    private static List<LeakSensor> LeakSensors(JsonElement data)
    {
        var sensors = new List<LeakSensor>();
        for (var channel = 1; channel <= LeakChannels; channel++)
        {
            if (Number(data, $"leak{channel}") is double value and (0 or 1))
            {
                sensors.Add(new LeakSensor(channel, value == 1));
            }
        }

        return sensors;
    }

    // Ambient removed its outdoor AQI fields in 2023, so the only AQI is indoor: from the PM2.5 indoor
    // sensor, or the AQIN monitor. `co2` is a standalone CO2 meter; the AQIN reports indoor CO2 separately.
    private static AirQuality? ReadAirQuality(JsonElement data)
    {
        var air = new AirQuality(
            Number(data, "pm25"),
            Number(data, "pm25_24h"),
            Number(data, "pm25_in") ?? Number(data, "pm25_in_aqin"),
            Number(data, "aqi_pm25_in") ?? Number(data, "aqi_pm25_aqin"),
            Number(data, "co2"),
            Number(data, "co2_in_aqin"));
        return air is { Pm25: null, Pm25Avg24h: null, IndoorPm25: null, IndoorAqi: null, Co2Ppm: null, IndoorCo2Ppm: null }
            ? null
            : air;
    }

    // Ambient gives the last strike's distance in miles. Its time is epoch milliseconds, like `dateutc`,
    // though the specs only call it a datetime, so an ISO string is read too.
    private static Lightning? ReadLightning(JsonElement data)
    {
        var today = Number(data, "lightning_day");
        var hour = Number(data, "lightning_hour");
        DateTimeOffset? last = data.TryGetProperty("lightning_time", out var time)
            ? time.ValueKind switch
            {
                JsonValueKind.Number when time.GetDouble() > 0 => DateTimeOffset.FromUnixTimeMilliseconds((long)time.GetDouble()),
                JsonValueKind.String when time.TryGetDateTimeOffset(out var parsed) => parsed,
                _ => null,
            }
            : null;
        var distance = Number(data, "lightning_distance");

        return today is null && hour is null && last is null && distance is null
            ? null
            : new Lightning(today, hour, last, distance);
    }

    private static List<string> LowBatteries(JsonElement data) =>
        Batteries
            .Where(battery => BatteryLow(data, battery.Field, battery.OneIsLow) == true)
            .Select(battery => battery.Name)
            .ToList();

    // Most of Ambient's battery fields are 1 for OK and 0 for low; `oneIsLow` flips that for the ones that aren't.
    private static bool? BatteryLow(JsonElement data, string name, bool oneIsLow = false) =>
        Number(data, name) is double battery ? battery == (oneIsLow ? 1 : 0) : null;

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
                Number(data, "hourlyrainin"),
                Number(data, "humidity"),
                Number(data, "dewPoint"),
                Number(data, "uv"),
                Number(data, "solarradiation"),
                Number(data, "tempinf")))
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
