using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class StationTests
{
    private const string Mac = "AA:BB:CC:DD:EE:FF";

    private static readonly long NowMs = TestData.Now.ToUnixTimeMilliseconds();

    private static readonly string Devices = $$"""
        [{
            "macAddress": "{{Mac}}",
            "apiKey": "secret-api",
            "info": {
                "name": "Backyard",
                "address": "1 Private Street",
                "coords": { "coords": { "lat": 43.1234, "lon": -69.5678 } }
            },
            "lastData": {
                "dateutc": {{NowMs}},
                "tempf": 66.9,
                "feelsLike": 65.1,
                "dewPoint": 34.45,
                "humidity": 30,
                "tempinf": 74.1,
                "humidityin": 31,
                "windspeedmph": 0.9,
                "windgustmph": 4,
                "winddir": 58,
                "maxdailygust": 5,
                "baromrelin": 30.05,
                "hourlyrainin": 0.1,
                "eventrainin": 0.2,
                "dailyrainin": 0.3,
                "weeklyrainin": 0.4,
                "monthlyrainin": 0.5,
                "yearlyrainin": 0.6,
                "lastRain": "2026-10-29T12:00:00.000Z",
                "uv": 2,
                "solarradiation": 120.5,
                "battout": 0,
                "windspdmph_avg2m": 1.2,
                "winddir_avg2m": 60,
                "windspdmph_avg10m": 1.5,
                "winddir_avg10m": 70,
                "windgustdir": 80,
                "24hourrainin": 0.7,
                "totalrainin": 12.34,
                "temp2f": 40.5,
                "humidity2": 55,
                "batt2": 0,
                "soiltemp1f": 50.1,
                "soilhum1": 33,
                "leak1": 0,
                "leak2": 1,
                "leak3": 2,
                "pm25": 8.5,
                "pm25_24h": 7.1,
                "aqi_pm25_in": 35,
                "co2": 612,
                "co2_in_aqin": 540,
                "lightning_day": 4,
                "lightning_hour": 1,
                "lightning_time": {{NowMs - 600_000}},
                "lightning_distance": 10,
                "battin": 1,
                "batt_lightning": 0,
                "batleak1": 0,
                "batleak2": 1,
                "battsm1": 0
            }
        }]
        """;

    private static readonly string History = $$"""
        [
            { "dateutc": {{NowMs}}, "tempf": 66.9, "windspeedmph": 1, "windgustmph": 2, "baromrelin": 30.1, "hourlyrainin": 0, "humidity": 30, "dewPoint": 34.5, "uv": 2, "solarradiation": 120.5, "tempinf": 74.1 },
            { "dateutc": {{NowMs - 300_000}}, "tempf": 65.5 },
            { "dateutc": {{NowMs - 90_000_000}}, "tempf": 50 }
        ]
        """;

    private static IOptions<WeatherOptions> Keys(string? macAddress = null) => TestData.Options(options =>
    {
        options.AmbientWeather.ApiKey = "secret-api";
        options.AmbientWeather.ApplicationKey = "secret-app";
        options.AmbientWeather.MacAddress = macAddress;
        options.AmbientWeather.RequestSpacingMilliseconds = 0;
    });

    private static AmbientWeatherStation Station(StubHttp http, string? macAddress = null) =>
        new(http, Keys(macAddress), new FixedTime(TestData.Now));

    private static StationService Service(StubHttp http, WeatherCache? cache = null, DateTimeOffset? now = null)
    {
        var options = Keys();
        var time = new FixedTime(now ?? TestData.Now);
        return new StationService(
            new StationDirectory([new AmbientWeatherStation(http, options, time)], http, options, TestData.Locations(http, options), TimeProvider.System),
            cache ?? new WeatherCache(),
            options,
            time,
            TestData.Sky(http, options),
            NullLogger<StationService>.Instance);
    }

    private static StubHttp Working() =>
        StubHttp.Json(url => url.Contains("/devices?") ? Devices : History);

    private static StubHttp Failing(Func<string, bool>? fails = null) =>
        new(request => fails is null || fails(request.RequestUri!.ToString())
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.ToString().Contains("/devices?") ? Devices : History),
            });

    [Fact]
    public async Task ParsesCurrentReadings()
    {
        var reading = await Station(Working()).GetCurrentAsync(default);

        Assert.Equal("Backyard", reading.StationName);
        Assert.Equal(TestData.Now, reading.ObservedAt);
        Assert.Equal(66.9, reading.TemperatureF);
        Assert.Equal(74.1, reading.IndoorTemperatureF);
        Assert.Equal(31, reading.IndoorHumidityPercent);
        Assert.Equal(58, reading.WindDirectionDegrees);
        Assert.Equal(30.05, reading.PressureInHg);
        Assert.Equal(0.3, reading.DailyRainIn);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero), reading.LastRain);
        Assert.True(reading.BatteryLow);
    }

    [Fact]
    public async Task ToleratesMissingFields()
    {
        var http = StubHttp.Json(_ => $$$"""[{"macAddress":"{{{Mac}}}","lastData":{"dateutc":{{{NowMs}}}}}]""");

        var reading = await Station(http).GetCurrentAsync(default);

        Assert.Null(reading.StationName);
        Assert.Null(reading.TemperatureF);
        Assert.Null(reading.BatteryLow);
        Assert.Null(reading.TotalRainIn);
        Assert.Empty(reading.ExtraSensors);
        Assert.Empty(reading.SoilSensors);
        Assert.Empty(reading.LeakSensors);
        Assert.Null(reading.AirQuality);
        Assert.Null(reading.Lightning);
        Assert.Empty(reading.LowBatteries);
    }

    [Fact]
    public async Task ParsesWindAndRainExtras()
    {
        var reading = await Station(Working()).GetCurrentAsync(default);

        Assert.Equal(1.2, reading.WindSpeedAvg2mMph);
        Assert.Equal(60, reading.WindDirectionAvg2mDegrees);
        Assert.Equal(1.5, reading.WindSpeedAvg10mMph);
        Assert.Equal(70, reading.WindDirectionAvg10mDegrees);
        Assert.Equal(80, reading.WindGustDirectionDegrees);
        Assert.Equal(0.7, reading.Rain24HourIn);
        Assert.Equal(12.34, reading.TotalRainIn);
    }

    [Fact]
    public async Task ParsesAddOnSensorsThatReport()
    {
        var reading = await Station(Working()).GetCurrentAsync(default);

        Assert.Equal([new ExtraSensor(2, 40.5, 55, true)], reading.ExtraSensors);
        Assert.Equal([new SoilSensor(1, 50.1, 33)], reading.SoilSensors);
        Assert.Equal([new LeakSensor(1, false), new LeakSensor(2, true)], reading.LeakSensors);
        Assert.Equal(new AirQuality(8.5, 7.1, null, 35, 612, 540), reading.AirQuality);
        Assert.Equal(new Lightning(4, 1, TestData.Now.AddMinutes(-10), 10), reading.Lightning);
        // batt_lightning and batleak* are 1 for low, unlike battin and battsm*, so only these two are low.
        Assert.Equal(["leak sensor 2", "soil sensor 1"], reading.LowBatteries);
    }

    [Theory]
    [InlineData("batt_lightning", 0, false)]
    [InlineData("batt_lightning", 1, true)]
    [InlineData("batleak3", 0, false)]
    [InlineData("batleak3", 1, true)]
    [InlineData("battin", 1, false)]
    [InlineData("battin", 0, true)]
    [InlineData("battsm2", 0, true)]
    public async Task BatteryFlagsFollowEachFieldsPolarity(string field, int value, bool low)
    {
        var http = StubHttp.Json(_ => $$$"""[{"macAddress":"{{{Mac}}}","lastData":{"dateutc":{{{NowMs}}},"{{{field}}}":{{{value}}}}}]""");

        var reading = await Station(http).GetCurrentAsync(default);

        Assert.Equal(low, reading.LowBatteries.Count == 1);
    }

    [Fact]
    public async Task LightningTimeCanBeAString()
    {
        var http = StubHttp.Json(_ => $$$"""[{"macAddress":"{{{Mac}}}","lastData":{"dateutc":{{{NowMs}}},"lightning_time":"2026-11-08T09:30:00Z"}}]""");

        var reading = await Station(http).GetCurrentAsync(default);

        Assert.Equal(new DateTimeOffset(2026, 11, 8, 9, 30, 0, TimeSpan.Zero), reading.Lightning?.LastStrike);
    }

    [Fact]
    public async Task HistoryIsOldestFirstAndLimitedToOneDay()
    {
        var http = Working();

        var history = await Station(http).GetHistoryAsync(default);

        Assert.Equal([65.5, 66.9], history.Select(point => point.TemperatureF));
        Assert.Null(history[0].WindSpeedMph);
        Assert.Equal(new StationHistoryPoint(TestData.Now, 66.9, 1, 2, 30.1, 0, 30, 34.5, 2, 120.5, 74.1), history[1]);
        Assert.Contains(http.Requests, request =>
            request.RequestUri!.ToString().Contains("/devices/AA%3ABB%3ACC%3ADD%3AEE%3AFF?")
            && request.RequestUri.Query.Contains("limit=288"));
    }

    [Fact]
    public async Task PicksTheConfiguredDevice()
    {
        var http = StubHttp.Json(_ => $$$"""
            [{"macAddress":"11:11:11:11:11:11","lastData":{"dateutc":{{{NowMs}}},"tempf":1}},
             {"macAddress":"{{{Mac}}}","lastData":{"dateutc":{{{NowMs}}},"tempf":2}}]
            """);

        var reading = await Station(http, Mac.ToLowerInvariant()).GetCurrentAsync(default);

        Assert.Equal(2, reading.TemperatureF);
    }

    [Fact]
    public async Task EmptyDeviceListIsAnError()
    {
        var report = (await Service(StubHttp.Json(_ => "[]")).GetReportAsync())!;

        Assert.Equal(StationStatus.Error, report.Status);
        Assert.Null(report.Current);
    }

    [Theory]
    [InlineData(null, "app")]
    [InlineData("api", null)]
    public async Task EitherMissingKeyNeedsKey(string? apiKey, string? applicationKey)
    {
        var http = Working();
        var options = TestData.Options(settings =>
        {
            settings.AmbientWeather.ApiKey = apiKey;
            settings.AmbientWeather.ApplicationKey = applicationKey;
        });
        var time = new FixedTime(TestData.Now);
        var service = new StationService(
            new StationDirectory([new AmbientWeatherStation(http, options, time)], http, options, TestData.Locations(http, options), TimeProvider.System),
            new WeatherCache(),
            options,
            time,
            TestData.Sky(http, options),
            NullLogger<StationService>.Instance);

        var report = await service.GetReportAsync();

        Assert.Equal(StationStatus.NeedsKey, report!.Status);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task HealthyStationIsOk()
    {
        var report = (await Service(Working()).GetReportAsync())!;

        Assert.Equal(StationStatus.Ok, report.Status);
        Assert.Null(report.Message);
        Assert.Equal(66.9, report.Current!.TemperatureF);
        Assert.Equal(2, report.History.Count);
        Assert.Equal(66.9, report.Trends!.HighF);
        Assert.Equal(65.5, report.Trends.LowF);
        Assert.Equal(TestData.Now.AddMinutes(-5), report.Trends.LowAt);
    }

    [Fact]
    public async Task OldReadingIsOffline()
    {
        var report = (await Service(Working(), now: TestData.Now.AddMinutes(16)).GetReportAsync())!;

        Assert.Equal(StationStatus.Offline, report.Status);
        Assert.NotNull(report.Current);
        Assert.Null(report.Trends);
    }

    [Fact]
    public async Task FailureFallsBackToLastGoodData()
    {
        var cache = new WeatherCache();
        cache.Set("last-good:station:home:current", new StationReading { ObservedAt = TestData.Now, TemperatureF = 50 }, TimeSpan.FromHours(1));

        var report = (await Service(Failing(), cache).GetReportAsync())!;

        Assert.Equal(StationStatus.Error, report.Status);
        Assert.Equal(50, report.Current!.TemperatureF);
        Assert.Equal("Showing last good data. Source returned HTTP 429", report.Message);
    }

    [Fact]
    public async Task HistoryFailureKeepsCurrentReadings()
    {
        var report = (await Service(Failing(url => url.Contains("limit="))).GetReportAsync())!;

        Assert.Equal(StationStatus.Ok, report.Status);
        Assert.NotNull(report.Current);
        Assert.Empty(report.History);
        Assert.Equal("Source returned HTTP 429", report.HistoryMessage);
    }

    [Fact]
    public async Task ReportNeverContainsPrivateFields()
    {
        var json = JsonSerializer.Serialize(await Service(Working()).GetReportAsync());

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain(Mac, json);
        Assert.DoesNotContain("Private Street", json);
        Assert.DoesNotContain("43.1234", json);
    }
}
