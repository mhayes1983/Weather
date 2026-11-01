using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class WeatherUndergroundStationTests
{
    private const string Id = "KMETEST1";

    private static string Current(double temp = 60) => $$$"""
        {"observations":[{
            "stationID": "{{{Id}}}",
            "obsTimeUtc": "2026-10-30T16:00:00Z",
            "neighborhood": "Private Road",
            "lat": 43.1234,
            "lon": -69.5678,
            "solarRadiation": 120.5,
            "uv": 2,
            "winddir": 90,
            "humidity": 45,
            "imperial": {
                "temp": {{{temp}}},
                "heatIndex": 72,
                "dewpt": 40,
                "windChill": 38,
                "windSpeed": 6,
                "windGust": 12,
                "pressure": 30.1,
                "precipRate": 0.1,
                "precipTotal": 0.25
            }
        }]}
        """;

    private const string History = """
        {"observations":[
            {"obsTimeUtc":"2026-10-30T15:55:00Z","imperial":{"tempAvg":59,"windspeedAvg":5,"windgustHigh":9,"pressureMax":30.2,"pressureMin":30.0,"precipRate":0}},
            {"obsTimeUtc":"2026-10-30T15:50:00Z","imperial":{"tempAvg":58}}
        ]}
        """;

    private static IOptions<WeatherOptions> Options(string? key = "secret-wu") => TestData.Options(options =>
    {
        options.WeatherUnderground.ApiKey = key;
        options.AmbientWeather.ApiKey = "secret-api";
        options.AmbientWeather.ApplicationKey = "secret-app";
        options.AmbientWeather.RequestSpacingMilliseconds = 0;
    });

    private static WeatherUndergroundStation Station(StubHttp http, string? name = "Test", string? key = "secret-wu") =>
        new(new WeatherStationOptions { Id = Id, Name = name }, http, Options(key));

    private static StationService Service(StubHttp http, string? key = "secret-wu")
    {
        var options = Options(key);
        var time = new FixedTime(TestData.Now);
        return new StationService(
            new StationDirectory([
                new AmbientWeatherStation(http, options, time),
                new WeatherUndergroundStation(new WeatherStationOptions { Id = Id, Name = "Test" }, http, options),
            ], http, options, TestData.Locations(http, options), TimeProvider.System),
            new WeatherCache(),
            options,
            time,
            NullLogger<StationService>.Instance);
    }

    private static StubHttp Working() =>
        StubHttp.Json(url => url.Contains("all/1day") ? History : Current());

    [Fact]
    public async Task ParsesCurrentReadings()
    {
        var http = Working();

        var reading = await Station(http).GetCurrentAsync(default);

        Assert.Equal("Test", reading.StationName);
        Assert.Equal(TestData.Now, reading.ObservedAt);
        Assert.Equal(60, reading.TemperatureF);
        Assert.Equal(60, reading.FeelsLikeF);
        Assert.Equal(40, reading.DewPointF);
        Assert.Equal(45, reading.HumidityPercent);
        Assert.Equal(12, reading.WindGustMph);
        Assert.Equal(90, reading.WindDirectionDegrees);
        Assert.Equal(30.1, reading.PressureInHg);
        Assert.Equal(0.25, reading.DailyRainIn);
        Assert.Null(reading.IndoorTemperatureF);
        Assert.Contains($"stationId={Id}", http.Requests[0].RequestUri!.Query);
        Assert.Contains("numericPrecision=decimal", http.Requests[0].RequestUri!.Query);
    }

    [Theory]
    [InlineData(45, 38)]
    [InlineData(75, 72)]
    [InlineData(50, 50)]
    [InlineData(68, 68)]
    public async Task FeelsLikeFollowsTheTemperature(double temp, double expected)
    {
        var reading = await Station(StubHttp.Json(_ => Current(temp))).GetCurrentAsync(default);

        Assert.Equal(expected, reading.FeelsLikeF);
    }

    [Fact]
    public async Task ToleratesMissingFields()
    {
        var http = StubHttp.Json(_ => """{"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","uv":null,"imperial":{"temp":null}}]}""");

        var reading = await Station(http).GetCurrentAsync(default);

        Assert.Null(reading.TemperatureF);
        Assert.Null(reading.FeelsLikeF);
        Assert.Null(reading.UvIndex);
    }

    [Fact]
    public async Task HistoryIsOldestFirstWithMidpointPressure()
    {
        var http = Working();

        var history = await Station(http).GetHistoryAsync(default);

        Assert.Contains("numericPrecision=decimal", http.Requests[0].RequestUri!.Query);
        Assert.Equal([58, 59], history.Select(point => point.TemperatureF));
        Assert.Null(history[0].PressureInHg);
        Assert.Equal(30.1, history[1].PressureInHg!.Value, 5);
    }

    [Fact]
    public async Task EmptyResponseMeansNoRecentData()
    {
        var report = (await Service(StubHttp.Json(url => url.Contains("/devices") ? "[]" : "")).GetReportAsync(Id))!;

        Assert.Equal(StationStatus.Error, report.Status);
        Assert.Equal("The station hasn't reported in the past hour.", report.Message);
        Assert.Empty(report.History);
    }

    [Fact]
    public void NameFallsBackToTheId() =>
        Assert.Equal(Id, Station(Working(), name: null).Name);

    [Fact]
    public async Task ReportsTheSelectedStationAndListsHomeFirst()
    {
        var service = Service(Working());

        var report = (await service.GetReportAsync(Id))!;

        Assert.Equal(["home", Id], service.Stations.Select(station => station.Id));
        Assert.Equal("Weather Underground", report.Source);
        Assert.Equal(StationHistoryRange.Today, report.HistoryRange);
        Assert.Equal(60, report.Current!.TemperatureF);
    }

    [Fact]
    public async Task MalformedIdReturnsNull()
    {
        var http = Working();

        Assert.Null(await Service(http).GetReportAsync("not valid!"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task UnconfiguredStationsUseTheirPublicName()
    {
        var options = Options();
        var http = StubHttp.Json(_ => Current().Replace("Private Road", "Harpswell"));
        var station = new WeatherUndergroundStation(new WeatherStationOptions { Id = Id }, http, options, lookedUp: true);

        var reading = await station.GetCurrentAsync(default);

        Assert.Equal("Harpswell", reading.StationName);
    }

    [Fact]
    public async Task ConfiguredStationsWithoutANameShowTheirId()
    {
        var reading = await Station(Working(), name: null).GetCurrentAsync(default);

        Assert.Equal(Id, reading.StationName);
    }

    [Fact]
    public async Task RepeatRequestsUseTheCache()
    {
        var http = Working();
        var service = Service(http);

        await service.GetReportAsync(Id);
        await service.GetReportAsync(Id);

        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task FailureDoesNotAffectTheHomeStation()
    {
        var http = new StubHttp(request => request.RequestUri!.Host == "api.weather.com"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri.Query.Contains("limit=")
                    ? "[]"
                    : $$$"""[{"macAddress":"AA","lastData":{"dateutc":{{{TestData.Now.ToUnixTimeMilliseconds()}}},"tempf":50}}]"""),
            });
        var service = Service(http);

        var other = (await service.GetReportAsync(Id))!;
        var home = (await service.GetReportAsync())!;

        Assert.Equal(StationStatus.Error, other.Status);
        Assert.Equal("Source returned HTTP 401", other.Message);
        Assert.Equal(StationStatus.Ok, home.Status);
    }

    [Fact]
    public async Task MissingKeyOnlyAffectsWeatherUndergroundStations()
    {
        var http = StubHttp.Json(_ => "[]");

        var report = (await Service(http, key: null).GetReportAsync(Id))!;

        Assert.Equal(StationStatus.NeedsKey, report.Status);
        Assert.DoesNotContain(http.Requests, request => request.RequestUri!.Host == "api.weather.com");
    }

    [Theory]
    [InlineData(20, StationStatus.Ok)]
    [InlineData(31, StationStatus.Offline)]
    public async Task SlowerStationsHaveTheirOwnOfflineThreshold(int minutesOld, StationStatus expected)
    {
        var options = Options();
        var http = Working();
        var time = new FixedTime(TestData.Now.AddMinutes(minutesOld));
        var service = new StationService(
            new StationDirectory([new WeatherUndergroundStation(new WeatherStationOptions { Id = Id }, http, options)], http, options, TestData.Locations(http, options), TimeProvider.System),
            new WeatherCache(),
            options,
            time,
            NullLogger<StationService>.Instance);

        var report = (await service.GetReportAsync(Id))!;

        Assert.Equal(expected, report.Status);
    }

    [Fact]
    public async Task FailureFallsBackToLastGoodData()
    {
        var options = Options();
        var cache = new WeatherCache();
        cache.Set($"last-good:station:{Id}:current", new StationReading { ObservedAt = TestData.Now, TemperatureF = 50 }, TimeSpan.FromHours(1));
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var service = new StationService(
            new StationDirectory([new WeatherUndergroundStation(new WeatherStationOptions { Id = Id }, http, options)], http, options, TestData.Locations(http, options), TimeProvider.System),
            cache,
            options,
            new FixedTime(TestData.Now),
            NullLogger<StationService>.Instance);

        var report = (await service.GetReportAsync(Id))!;

        Assert.Equal(StationStatus.Error, report.Status);
        Assert.Equal(50, report.Current!.TemperatureF);
        Assert.Equal("Showing last good data. Source returned HTTP 429", report.Message);
    }

    [Fact]
    public void RegistrationListsHomeFirstAndSkipsBadEntries()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stations:0:Id"] = "KFIRST1",
                ["Stations:1:Id"] = " ",
                ["Stations:2:Id"] = "home",
                ["Stations:3:Id"] = "KSECOND2",
                ["Stations:3:Name"] = "Second",
                ["Stations:4:Id"] = "kfirst1",
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddWeatherCore(configuration)
            .BuildServiceProvider();

        var stations = provider.GetRequiredService<StationService>().Stations;

        Assert.Equal(
            [new StationSummary("home", "Home station"), new StationSummary("KFIRST1", "KFIRST1"), new StationSummary("KSECOND2", "Second")],
            stations);
    }

    [Fact]
    public async Task ReportNeverContainsPrivateFields()
    {
        var json = JsonSerializer.Serialize(await Service(Working()).GetReportAsync(Id));

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("Private Road", json);
        Assert.DoesNotContain("43.1234", json);
    }
}
