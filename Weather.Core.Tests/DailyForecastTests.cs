using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.DailyForecast;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class DailyForecastTests
{
    private const string Id = "KMETEST1";
    private static readonly DateOnly Today = new(2026, 10, 30);
    private static readonly DateOnly Tomorrow = new(2026, 10, 31);
    private static readonly DateOnly DayAfter = new(2026, 11, 1);

    private const string Outlook = """
        {
            "timezone": "America/New_York",
            "utc_offset_seconds": -14400,
            "daily": {
                "time": ["2026-10-30", "2026-10-31", "2026-11-01"],
                "weather_code": [3, 61, null],
                "temperature_2m_max": [50.4, 57.6, 60],
                "temperature_2m_min": [34.3, 29.4, null],
                "sunrise": ["2026-10-30T07:14", "2026-10-31T07:15", "2026-11-01T07:16"],
                "sunset": ["2026-10-30T17:31", "2026-10-31T17:30", "2026-11-01T16:29"],
                "precipitation_sum": [0, 0.17, 0],
                "precipitation_probability_max": [0, 68, 0],
                "wind_speed_10m_max": [13.1, 8.1, 5],
                "wind_direction_10m_dominant": [322, 247, 90],
                "uv_index_max": [3.45, 3.35, 3]
            },
            "hourly": {
                "time": ["2026-10-30T11:00", "2026-10-30T12:00", "2026-10-30T13:00", "2026-10-31T23:00", "2026-11-01T00:00"],
                "weather_code": [0, 0, 0, 2, 2],
                "is_day": [1, 1, 1, 0, 0],
                "temperature_2m": [48.6, 49, 50, 40, null],
                "apparent_temperature": [44.2, 45, 46, 38, null],
                "precipitation_probability": [0, 1, 2, 3, null],
                "precipitation": [0, 0, 0.01, 0, null],
                "cloud_cover": [7, 8, 9, 10, null],
                "dew_point_2m": [22, 22, 23, 24, null],
                "relative_humidity_2m": [34, 33, 32, 60, null],
                "wind_speed_10m": [11.2, 10, 9, 3, null],
                "wind_direction_10m": [315, 300, 290, 280, null],
                "pressure_msl": [1026.7, 1026.6, 1026.5, 1030, null]
            }
        }
        """;

    private const string Points = """{"properties":{"forecast":"https://api.weather.gov/gridpoints/GYX/80,72/forecast"}}""";

    private const string Periods = """
        {"properties":{"periods":[
            {"startTime":"2026-10-30T02:00:00-04:00","isDaytime":false,"detailedForecast":"Rest of last night."},
            {"startTime":"2026-10-30T06:00:00-04:00","isDaytime":true,"detailedForecast":"Sunny today."},
            {"startTime":"2026-10-30T18:00:00-04:00","isDaytime":false,"detailedForecast":"Clear tonight."},
            {"startTime":"2026-10-31T06:00:00-04:00","isDaytime":true,"detailedForecast":"Rain tomorrow."}
        ]}}
        """;

    private const string Moon = """
        {"properties":{"data":{
            "curphase": "Waning Crescent",
            "fracillum": "41%",
            "moondata": [{"phen":"Upper Transit","time":"06:15"},{"phen":"Set","time":"13:27"}]
        }}}
        """;

    private const string StationLocation = """
        {"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","lat":42.3601,"lon":-71.0589,"neighborhood":"Private Road","imperial":{}}]}
        """;

    private static string Respond(string url) =>
        url.Contains("api.open-meteo.com") ? Outlook
        : url.Contains("/points/") ? Points
        : url.Contains("gridpoints") ? Periods
        : url.Contains("aa.usno.navy.mil") ? Moon
        : StationLocation;

    private static IOptions<WeatherOptions> Options(string? key = "secret-wu") => TestData.Options(options =>
    {
        options.WeatherUnderground.ApiKey = key;
    });

    private static DailyForecastService Service(
        StubHttp http,
        WeatherCache? cache = null,
        string? key = "secret-wu",
        DateTimeOffset? now = null,
        ILogger<DailyForecastService>? logger = null)
    {
        var options = Options(key);
        var time = new FixedTime(now ?? TestData.Now);
        cache ??= new WeatherCache();
        return new DailyForecastService(
            [
                new AmbientWeatherStation(http, options, time),
                new WeatherUndergroundStation(new WeatherStationOptions { Id = Id, Name = "Test" }, http, options),
            ],
            new StationLocator(cache),
            new OpenMeteoOutlook(http, options),
            new NwsDescriptions(http, cache),
            new UsnoMoon(http),
            cache,
            options,
            time,
            logger ?? NullLogger<DailyForecastService>.Instance);
    }

    private static StubHttp Failing(Func<string, bool> fails) =>
        new(request =>
        {
            var url = request.RequestUri!.ToString();
            return fails(url)
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respond(url)) };
        });

    [Fact]
    public async Task ParsesDailyData()
    {
        var report = (await Service(StubHttp.Json(Respond)).GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal("Home station", report.StationName);
        Assert.Equal(3, report.Days.Count);

        var day = report.Days[1];
        Assert.Equal(Tomorrow, day.Date);
        Assert.Equal("Light Rain", day.Conditions);
        Assert.Equal("rain", day.Icon);
        Assert.Equal(58, day.HighF);
        Assert.Equal(29, day.LowF);
        Assert.Equal(68, day.PrecipChancePercent);
        Assert.Equal(0.17, day.PrecipIn);
        Assert.Equal(247, day.WindDirectionDegrees);
        Assert.Equal("2026-10-31T07:15", day.Sunrise);
    }

    [Fact]
    public async Task TimesAfterAClockChangeAreCorrected()
    {
        var report = (await Service(StubHttp.Json(Respond)).GetAsync())!;

        Assert.Equal("2026-10-31T07:15", report.Days[1].Sunrise);
        Assert.Equal("2026-11-01T06:16", report.Days[2].Sunrise);
    }

    [Fact]
    public async Task HourlyTimesAfterAClockChangeMoveToTheCorrectDay()
    {
        var body = Outlook.Replace("2026-11-01T00:00", "2026-11-02T00:00").Replace("[48.6, 49, 50, 40, null]", "[48.6, 49, 50, 40, 39]");
        var http = StubHttp.Json(url => url.Contains("open-meteo") ? body : Respond(url));

        var report = (await Service(http).GetAsync())!;
        var detail = (await Service(http).GetDayAsync(DayAfter))!;

        Assert.True(report.Days[2].HasHourly);
        Assert.Equal("2026-11-01T23:00", Assert.Single(detail.Hours).Time);
    }

    [Fact]
    public async Task DaysWithoutHourlyValuesCannotBeOpened()
    {
        var service = Service(StubHttp.Json(Respond));

        var report = (await service.GetAsync())!;

        Assert.Equal([true, true, false], report.Days.Select(day => day.HasHourly));
        Assert.Null(report.Days[2].LowF);
        Assert.Null(await service.GetDayAsync(DayAfter));
    }

    [Fact]
    public async Task DateOutsideTheForecastIsRejected() =>
        Assert.Null(await Service(StubHttp.Json(Respond)).GetDayAsync(new DateOnly(2026, 12, 25)));

    [Fact]
    public async Task UnknownStationIsRejected()
    {
        var http = StubHttp.Json(Respond);
        var service = Service(http);

        Assert.Null(await service.GetAsync("KOTHER1"));
        Assert.Null(await service.GetDayAsync(Today, "KOTHER1"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task HourlyTableMatchesTheScreenshotColumns()
    {
        var detail = (await Service(StubHttp.Json(Respond)).GetDayAsync(Tomorrow))!;

        var hour = Assert.Single(detail.Hours);
        Assert.Equal("2026-10-31T23:00", hour.Time);
        Assert.Equal("Partly Cloudy", hour.Conditions);
        Assert.Equal("partly-cloudy-night", hour.Icon);
        Assert.Equal(40, hour.TemperatureF);
        Assert.Equal(38, hour.FeelsLikeF);
        Assert.Equal(10, hour.CloudCoverPercent);
        Assert.Equal(24, hour.DewPointF);
        Assert.Equal(60, hour.HumidityPercent);
        Assert.Equal(30.42, hour.PressureInHg);
    }

    [Fact]
    public async Task TodayStartsAtTheCurrentHour()
    {
        // TestData.Now is 16:00 UTC, which is noon in New York.
        var detail = (await Service(StubHttp.Json(Respond)).GetDayAsync(Today))!;

        Assert.Equal(["2026-10-30T12:00", "2026-10-30T13:00"], detail.Hours.Select(hour => hour.Time));
    }

    [Fact]
    public async Task LinksOnlyToDaysThatCanBeOpened()
    {
        var service = Service(StubHttp.Json(Respond));

        var today = (await service.GetDayAsync(Today))!;
        var tomorrow = (await service.GetDayAsync(Tomorrow))!;

        Assert.Null(today.PreviousDate);
        Assert.Equal(Tomorrow, today.NextDate);
        Assert.Equal(Today, tomorrow.PreviousDate);
        Assert.Null(tomorrow.NextDate);
    }

    [Fact]
    public async Task DescriptionsMatchTheirDates()
    {
        var service = Service(StubHttp.Json(Respond));

        var today = (await service.GetDayAsync(Today))!;
        var tomorrow = (await service.GetDayAsync(Tomorrow))!;

        Assert.Equal("Sunny today.", today.DayDescription);
        Assert.Equal("Clear tonight.", today.NightDescription);
        Assert.Equal("Rain tomorrow.", tomorrow.DayDescription);
        Assert.Null(tomorrow.NightDescription);
    }

    [Fact]
    public async Task NightAfterMidnightBelongsToThePreviousDay()
    {
        var descriptions = new NwsDescriptions(StubHttp.Json(Respond), new WeatherCache());

        var results = await descriptions.GetAsync(new GeoPoint(43.9, -69.9), default);

        Assert.Equal("Rest of last night.", results[Today.AddDays(-1)].Night);
    }

    [Fact]
    public async Task MoonEventsCanBeMissing()
    {
        var detail = (await Service(StubHttp.Json(Respond)).GetDayAsync(Today))!;

        Assert.Equal(new MoonInfo("Waning Crescent", 41, null, "13:27"), detail.Moon);
    }

    [Theory]
    [InlineData("2026-10-31", "tz=-4")]
    [InlineData("2026-11-02", "tz=-5")]
    public async Task MoonOffsetFollowsTheClockChange(string date, string expected)
    {
        var http = StubHttp.Json(Respond);

        await new UsnoMoon(http).GetAsync(new GeoPoint(43.9281, -69.9656), DateOnly.Parse(date), "America/New_York", default);

        Assert.Contains(expected + "&", http.Requests[0].RequestUri!.Query);
        Assert.Contains("coords=43.93,-69.97", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task HomeStationUsesTheConfiguredLocation()
    {
        var http = StubHttp.Json(Respond);

        await Service(http).GetAsync();

        Assert.Contains("latitude=43.9281", http.Requests[0].RequestUri!.Query);
        Assert.DoesNotContain(http.Requests, request => request.RequestUri!.Host == "api.weather.com");
    }

    [Fact]
    public async Task OtherStationsUseTheirCoordinatesOnce()
    {
        var http = StubHttp.Json(Respond);
        var cache = new WeatherCache();
        var service = Service(http, cache);

        await service.GetAsync(Id);
        await service.GetAsync(Id);

        Assert.Single(http.Requests, request => request.RequestUri!.Host == "api.weather.com");
        Assert.Contains(http.Requests, request => request.RequestUri!.Query.Contains("latitude=42.3601"));
    }

    [Fact]
    public async Task MissingKeyOnlyAffectsOtherStations()
    {
        var http = StubHttp.Json(Respond);
        var service = Service(http, key: null);

        Assert.Equal(SourceStatus.NeedsKey, (await service.GetAsync(Id))!.Status);
        Assert.Equal(SourceStatus.Ok, (await service.GetAsync())!.Status);
    }

    [Fact]
    public async Task DescriptionAndMoonFailuresOnlyHideThoseSections()
    {
        var http = Failing(url => url.Contains("gridpoints") || url.Contains("usno"));

        var detail = (await Service(http).GetDayAsync(Today))!;

        Assert.Null(detail.DayDescription);
        Assert.Null(detail.Moon);
        Assert.NotEmpty(detail.Hours);
    }

    [Fact]
    public async Task DaysThatHavePassedAreDropped()
    {
        // 00:10 in New York on Oct 31, with a forecast fetched the day before.
        var service = Service(StubHttp.Json(Respond), now: new DateTimeOffset(2026, 10, 31, 4, 10, 0, TimeSpan.Zero));

        var report = (await service.GetAsync())!;
        var tomorrow = (await service.GetDayAsync(Tomorrow))!;

        Assert.Equal(Tomorrow, report.Today);
        Assert.Equal(Tomorrow, report.Days[0].Date);
        Assert.Null(await service.GetDayAsync(Today));
        Assert.Null(tomorrow.PreviousDate);
    }

    [Fact]
    public async Task ForecastSurvivesLaterFailures()
    {
        var failing = false;
        var http = new StubHttp(request =>
        {
            var url = request.RequestUri!.ToString();
            return failing && url.Contains("open-meteo")
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respond(url)) };
        });
        var cache = new WeatherCache();
        var service = Service(http, cache);

        await service.GetAsync();
        failing = true;
        cache.Remove("outlook:home");
        var report = (await service.GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("Showing last good data. Source returned HTTP 500", report.Message);
        Assert.Equal(3, report.Days.Count);
    }

    [Fact]
    public async Task DayPageReportsAForecastFailure()
    {
        var service = Service(Failing(url => url.Contains("open-meteo")));

        var exception = await Assert.ThrowsAsync<ForecastUnavailableException>(() => service.GetDayAsync(Today));

        Assert.Equal("Source returned HTTP 500", exception.Message);
    }

    [Fact]
    public async Task DayPageReportsAMissingKey()
    {
        var service = Service(StubHttp.Json(Respond), key: null);

        await Assert.ThrowsAsync<ForecastUnavailableException>(() => service.GetDayAsync(Today, Id));
    }

    [Fact]
    public async Task LogsNeverContainCoordinates()
    {
        var logger = new CapturingLogger();
        var http = Failing(url => url.Contains("gridpoints") || url.Contains("usno"));

        await Service(http, logger: logger).GetDayAsync(Today);

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("43.9") || message.Contains("69.9"));
    }

    [Fact]
    public async Task ForecastFailureIsReported()
    {
        var report = (await Service(Failing(url => url.Contains("open-meteo"))).GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("Source returned HTTP 500", report.Message);
        Assert.Empty(report.Days);
    }

    [Fact]
    public async Task StationWithoutRecentDataIsReported()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") });

        var report = (await Service(http).GetAsync(Id))!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("The station hasn't reported in the past hour.", report.Message);
    }

    [Theory]
    [InlineData(0, true, "Sunny", "clear")]
    [InlineData(0, false, "Clear", "clear-night")]
    [InlineData(2, false, "Partly Cloudy", "partly-cloudy-night")]
    [InlineData(95, true, "Thunderstorms", "storm")]
    [InlineData(123, true, "Unknown", "unknown")]
    [InlineData(null, true, "Unknown", "unknown")]
    public void DescribesWeatherCodes(int? code, bool isDay, string label, string icon) =>
        Assert.Equal((label, icon), WeatherConditions.Describe(code, isDay));

    [Fact]
    public async Task ReportsNeverContainPrivateFields()
    {
        var service = Service(StubHttp.Json(Respond));

        var json = JsonSerializer.Serialize(await service.GetAsync(Id))
            + JsonSerializer.Serialize(await service.GetDayAsync(Tomorrow, Id));

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("Private Road", json);
        Assert.DoesNotContain("42.36", json);
        Assert.DoesNotContain("71.05", json);
    }

    private sealed class CapturingLogger : ILogger<DailyForecastService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
