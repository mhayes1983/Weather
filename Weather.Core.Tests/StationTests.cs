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
                "battout": 0
            }
        }]
        """;

    private static readonly string History = $$"""
        [
            { "dateutc": {{NowMs}}, "tempf": 66.9, "windspeedmph": 1, "windgustmph": 2, "baromrelin": 30.1, "hourlyrainin": 0 },
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
            new StationDirectory([new AmbientWeatherStation(http, options, time)], http, options),
            cache ?? new WeatherCache(),
            options,
            time,
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
    }

    [Fact]
    public async Task HistoryIsOldestFirstAndLimitedToOneDay()
    {
        var http = Working();

        var history = await Station(http).GetHistoryAsync(default);

        Assert.Equal([65.5, 66.9], history.Select(point => point.TemperatureF));
        Assert.Null(history[0].WindSpeedMph);
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
            new StationDirectory([new AmbientWeatherStation(http, options, time)], http, options),
            new WeatherCache(),
            options,
            time,
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
    }

    [Fact]
    public async Task OldReadingIsOffline()
    {
        var report = (await Service(Working(), now: TestData.Now.AddMinutes(16)).GetReportAsync())!;

        Assert.Equal(StationStatus.Offline, report.Status);
        Assert.NotNull(report.Current);
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
