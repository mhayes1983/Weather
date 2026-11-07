using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Alerts;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Snow;

namespace Weather.Core.Tests;

public class SnowTests
{
    private static readonly TimeProvider Time = new FixedTime(TestData.Now);

    // 2026-10-30 is EDT (UTC-4), so local midnight is 04:00Z.
    private static readonly DateTimeOffset Midnight = new(2026, 10, 30, 4, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Snow. Low around 25. New snow accumulation of 1 to 3 inches possible.", 1, 3)]
    [InlineData("Snow likely. New snow accumulation of around an inch possible.", 1, 1)]
    [InlineData("Chance of snow. New snow accumulation of less than half an inch possible.", 0, 0.5)]
    [InlineData("Snow showers. Snow accumulations less than one inch.", 0, 1)]
    [InlineData("Snow. Snow accumulations around one inch.", 1, 1)]
    [InlineData("Heavy snow. Snow accumulations of 6 to 10 inches.", 6, 10)]
    [InlineData("New snow and sleet accumulation of 2 to 4 inches possible.", 2, 4)]
    [InlineData("Snow showers likely. Little or no snow accumulation expected.", 0, SnowText.LittleOrNoInches)]
    [InlineData("Total snowfall around 3 inches.", 3, 3)]
    public void ReadsSnowRanges(string text, double low, double high)
    {
        Assert.Equal((low, high), SnowText.Read(text));
    }

    [Theory]
    [InlineData("Mostly sunny, with a high near 45.")]
    [InlineData("Rain. New rainfall amounts between a quarter and half of an inch possible.")]
    [InlineData("Chance of snow 40%.")]
    [InlineData(null)]
    public void IgnoresTextWithoutSnowAmounts(string? text)
    {
        Assert.Null(SnowText.Read(text));
    }

    [Fact]
    public void TotalsWholeLocalDatesAndSharesPeriodsAcrossEdges()
    {
        SnowPeriod[] periods =
        [
            // 12 hours before the window: half inside counts for half.
            new(Midnight.AddHours(-6), Midnight.AddHours(6), 2, 4),
            new(Midnight.AddHours(12), Midnight.AddHours(13), 0.5, 0.5),
            // Ends after the window.
            new(Midnight.AddHours(47), Midnight.AddHours(49), 1, 1),
        ];

        var (start, end) = SnowWindow.Bounds(new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31), "America/New_York");
        var total = SnowWindow.Total(periods, start, end);

        Assert.Equal(Midnight, start);
        Assert.Equal(Midnight.AddHours(48), end);
        Assert.Equal((2.0, 3.0, false), total);
    }

    [Fact]
    public void MarksTotalsPartialWhenTheForecastEndsFirst()
    {
        SnowPeriod[] periods = [new(Midnight, Midnight.AddHours(24), 3, 3)];

        var (start, end) = SnowWindow.Bounds(new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 1), "America/New_York");

        Assert.Equal((3.0, 3.0, true), SnowWindow.Total(periods, start, end));
        Assert.Null(SnowWindow.Total(periods, end, end.AddDays(1)));
    }

    [Fact]
    public void MarksTotalsPartialWhenTheForecastStartsLate()
    {
        // Like NWS in the afternoon: its first period starts at 6pm, missing the morning.
        SnowPeriod[] periods = [new(Midnight.AddHours(18), Midnight.AddHours(48), 2, 4)];

        var (start, end) = SnowWindow.Bounds(new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31), "America/New_York");

        Assert.Equal((2.0, 4.0, true), SnowWindow.Total(periods, start, end));
    }

    [Fact]
    public async Task OpenMeteoModelsShareOneCallAndReadTheirOwnSnowfall()
    {
        var http = StubHttp.Json(_ => """
            { "hourly": {
              "time": ["2026-10-30T05:00", "2026-10-30T06:00", "2026-10-30T07:00"],
              "snowfall_gfs_seamless": [0.4, null, 0.2],
              "snowfall_gem_seamless": [1, 1, 1]
            } }
            """);
        string[] models = ["gfs_seamless", "gem_seamless"];
        var cache = new WeatherCache();
        OpenMeteoSnowSource Source(string model) =>
            new(new ColumnDescriptor(model, model), model, "https://api.open-meteo.com/v1/forecast", models, http, cache, TestData.Options(), Time);

        var gfs = await Source("gfs_seamless").GetSnowAsync(TestData.Topsham, default);
        var gem = await Source("gem_seamless").GetSnowAsync(TestData.Topsham, default);

        Assert.Equal(
            [
                new SnowPeriod(Midnight.AddHours(0), Midnight.AddHours(1), 0.4, 0.4),
                new SnowPeriod(Midnight.AddHours(2), Midnight.AddHours(3), 0.2, 0.2),
            ],
            gfs.Periods);
        Assert.Equal(3, gem.Periods.Count);
        var query = Assert.Single(http.Requests).RequestUri!.Query;
        Assert.Contains("hourly=snowfall", query);
        Assert.Contains("precipitation_unit=inch", query);
        Assert.Contains("timezone=GMT", query);
        Assert.Contains("models=gfs_seamless,gem_seamless", query);
    }

    [Fact]
    public async Task NwsUsesTextRangesAndGridAmountsForOtherPeriods()
    {
        var http = StubHttp.Json(url => url switch
        {
            _ when url.Contains("/points/") => """
                { "properties": { "forecast": "https://api.weather.gov/gridpoints/GYX/1,2/forecast", "forecastGridData": "https://api.weather.gov/gridpoints/GYX/1,2" } }
                """,
            _ when url.EndsWith("/forecast") => """
                { "properties": { "periods": [
                  { "startTime": "2026-10-30T18:00:00-04:00", "endTime": "2026-10-31T06:00:00-04:00", "detailedForecast": "Snow. New snow accumulation of 1 to 3 inches possible." },
                  { "startTime": "2026-10-31T06:00:00-04:00", "endTime": "2026-10-31T18:00:00-04:00", "detailedForecast": "Cloudy, with a high near 35." }
                ] } }
                """,
            _ => """
                { "properties": { "snowfallAmount": { "uom": "wmoUnit:mm", "values": [
                  { "validTime": "2026-10-30T22:00:00+00:00/PT6H", "value": 50.8 },
                  { "validTime": "2026-10-31T10:00:00+00:00/PT6H", "value": 25.4 },
                  { "validTime": "2026-10-31T16:00:00+00:00/P1D", "value": 0 }
                ] } } }
                """,
        });
        var source = new NwsSnowSource(http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetSnowAsync(TestData.Topsham, default);

        Assert.Equal(
            [
                new SnowPeriod(DateTimeOffset.Parse("2026-10-30T18:00:00-04:00"), DateTimeOffset.Parse("2026-10-31T06:00:00-04:00"), 1, 3),
                new SnowPeriod(DateTimeOffset.Parse("2026-10-31T10:00:00Z"), DateTimeOffset.Parse("2026-10-31T16:00:00Z"), 1, 1),
                new SnowPeriod(DateTimeOffset.Parse("2026-10-31T16:00:00Z"), DateTimeOffset.Parse("2026-11-01T16:00:00Z"), 0, 0),
            ],
            result.Periods);
    }

    private const string WeatherUndergroundJson = """
        {
          "validTimeLocal": ["2026-10-30T07:00:00-0400", "2026-10-31T07:00:00-0400"],
          "daypart": [{
            "qpfSnow": [null, 2.4, 0.6, 0],
            "narrative": [null, "Snow. Low 28F. Snow accumulations of 1 to 3 inches.", "Snow showers. Snow accumulations less than one inch.", "Clear."],
            "temperature": [null, 28, 35, 20]
          }]
        }
        """;

    [Fact]
    public async Task WeatherUndergroundUsesNarrativeRangesThenAmounts()
    {
        var http = StubHttp.Json(_ => WeatherUndergroundJson);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "key");

        var result = await new WeatherUndergroundSnowSource(http, new WeatherCache(), options, Time).GetSnowAsync(TestData.Topsham, default);

        var dayStart = DateTimeOffset.Parse("2026-10-30T07:00:00-04:00");
        Assert.Equal(
            [
                new SnowPeriod(dayStart.AddHours(12), dayStart.AddHours(24), 1, 3),
                new SnowPeriod(dayStart.AddHours(24), dayStart.AddHours(36), 0, 1),
                new SnowPeriod(dayStart.AddHours(36), dayStart.AddHours(48), 0, 0),
            ],
            result.Periods);
    }

    [Fact]
    public async Task WeatherUndergroundSnowAndTemperaturesShareOneCall()
    {
        var http = StubHttp.Json(_ => WeatherUndergroundJson);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "key");
        var cache = new WeatherCache();

        await new WeatherUndergroundSnowSource(http, cache, options, Time).GetSnowAsync(TestData.Topsham, default);
        await new Sources.WeatherUndergroundSource(http, cache, options, Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task WeatherApiConvertsCentimetresToInches()
    {
        var start = Midnight.ToUnixTimeSeconds();
        var http = StubHttp.Json(_ => $$"""
            { "forecast": { "forecastday": [ { "hour": [
              { "time_epoch": {{start}}, "snow_cm": 2.54 },
              { "time_epoch": {{start + 3600}}, "snow_cm": 0 }
            ] } ] } }
            """);
        var options = TestData.Options(o => o.WeatherApi.ApiKey = "key");

        var result = await new WeatherApiSnowSource(http, options, Time).GetSnowAsync(TestData.Topsham, default);

        Assert.Equal(
            [new SnowPeriod(Midnight, Midnight.AddHours(1), 1, 1), new SnowPeriod(Midnight.AddHours(1), Midnight.AddHours(2), 0, 0)],
            result.Periods);
        Assert.Contains("days=3", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task KeyedSourcesNeedKeysWithoutCalling()
    {
        var http = StubHttp.Json(_ => throw new InvalidOperationException("No request expected."));

        var weatherApi = await new WeatherApiSnowSource(http, TestData.Options(), Time).GetSnowAsync(TestData.Topsham, default);
        var underground = await new WeatherUndergroundSnowSource(http, new WeatherCache(), TestData.Options(), Time).GetSnowAsync(TestData.Topsham, default);

        Assert.Equal(SourceStatus.NeedsKey, weatherApi.Status);
        Assert.Equal(SourceStatus.NeedsKey, underground.Status);
        Assert.Empty(http.Requests);
    }

    private sealed class FakeSnowSource(string id, Func<SnowResult> fetch) : ISnowSource
    {
        public ColumnDescriptor Column { get; } = new(id, id.ToUpperInvariant());
        public TimeSpan CacheDuration => TimeSpan.FromMinutes(30);
        public bool AppliesTo(ForecastLocation location) => true;
        public Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken) => Task.FromResult(fetch());
    }

    private static SnowTableService Service(IEnumerable<ISnowSource> sources, StubHttp? http = null, WeatherCache? cache = null)
    {
        cache ??= new WeatherCache();
        var options = TestData.Options(o => o.SnowTowns = [new SnowTownOptions { Name = "Topsham", Zip = "04086" }]);
        http ??= StubHttp.Json(_ => throw new InvalidOperationException("No lookup expected."));
        return new SnowTableService(sources, new LocationResolver(http, cache, options), cache, options, Time, NullLogger<SnowTableService>.Instance);
    }

    [Fact]
    public async Task OneFailingSourceOnlyBlanksItsOwnRow()
    {
        var service = Service(
        [
            new FakeSnowSource("good", () => SnowResult.Ok([new SnowPeriod(Midnight, Midnight.AddHours(24), 2, 4)], TestData.Now)),
            new FakeSnowSource("bad", () => throw new HttpRequestException("Down", null, HttpStatusCode.ServiceUnavailable)),
        ]);
        var (request, _) = service.Parse(null, "2026-10-30", "2026-10-30");

        var table = await service.GetAsync(request!);

        Assert.Equal(new SnowTown("04086", "Topsham", null), Assert.Single(table.Towns));
        Assert.Equal(new SnowCell("good", "04086", 2, 4, false, SourceStatus.Ok, null), table.Cells[0]);
        Assert.Equal(SourceStatus.Error, table.Sources[1].Status);
        Assert.Equal("Source returned HTTP 503", table.Cells[1].Message);
    }

    [Fact]
    public async Task UnknownTownIsNamedWithoutBreakingTheTable()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = Service([new FakeSnowSource("good", () => SnowResult.Ok([], TestData.Now))], http);
        var (request, _) = service.Parse("04086,99999", null, null);

        var table = await service.GetAsync(request!);

        Assert.Equal("That ZIP code wasn't found.", table.Towns[1].Message);
        Assert.Equal("04086", Assert.Single(table.Cells).Zip);
    }

    [Theory]
    [InlineData("04086", null, null, null)]
    [InlineData("0408", null, null, "Enter 5-digit ZIP codes.")]
    [InlineData("04086", "2026-10-31", "2026-10-30", "The end date can't be before the start date.")]
    [InlineData("04086", "2026-10-28", "2026-10-30", "Choose dates from yesterday to 15 days ahead.")]
    [InlineData("04086", "2026-11-01", "2026-11-08", "Choose a range of 7 days or fewer.")]
    [InlineData("04086", "11/01/2026", null, "Enter dates as YYYY-MM-DD.")]
    public void ValidatesRequests(string zips, string? start, string? end, string? error)
    {
        var (request, message) = Service([]).Parse(zips, start, end);

        Assert.Equal(error, message);
        if (error is null)
        {
            Assert.Equal(new DateOnly(2026, 10, 30), request!.Start);
            Assert.Equal(new DateOnly(2026, 10, 31), request.End);
        }
    }

    [Fact]
    public async Task WinterAlertsAreListedOnceWithTheirTowns()
    {
        var cache = new WeatherCache();
        var options = TestData.Options();
        var http = StubHttp.Json(url => url.Contains("zippopotam")
            ? """{ "places": [ { "place name": "Brunswick", "state abbreviation": "ME", "latitude": "43.9", "longitude": "-69.96" } ] }"""
            : url.Contains("timezone=auto")
                ? """{ "timezone": "America/New_York" }"""
                : """
                  { "features": [
                    { "properties": { "id": "a1", "event": "Winter Weather Advisory", "severity": "Minor" } },
                    { "properties": { "id": "a2", "event": "Small Craft Advisory", "severity": "Minor" } }
                  ] }
                  """);
        var service = new SnowAlertService(
            new LocationResolver(http, cache, options), new NwsAlerts(http, Time), cache, options, NullLogger<SnowAlertService>.Instance);

        var report = await service.GetAsync(["04086", "04011"]);

        Assert.Equal(SourceStatus.Ok, report.Status);
        var alert = Assert.Single(report.Alerts);
        Assert.Equal("Winter Weather Advisory", alert.Alert.Event);
        Assert.Equal(["04086", "04011"], alert.Zips);
    }

    [Fact]
    public async Task FailedAlertChecksSayWhichTowns()
    {
        var cache = new WeatherCache();
        var options = TestData.Options();
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = new SnowAlertService(
            new LocationResolver(http, cache, options), new NwsAlerts(http, Time), cache, options, NullLogger<SnowAlertService>.Instance);

        var report = await service.GetAsync(["04086"]);

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("Alerts are unavailable right now.", report.Message);
        Assert.Empty(report.Alerts);
    }
}
