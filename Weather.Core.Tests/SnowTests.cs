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
        Assert.Equal(
            new SnowCell("good", "04086", 2, 4, false, SourceStatus.Ok, null, Midnight, Midnight.AddHours(24),
                TimingLowInches: 2, TimingHighInches: 4, PeakStart: Midnight, PeakEnd: Midnight.AddHours(24), PeakInchesPerHour: 0.17),
            table.Cells[0]);
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

    [Fact]
    public void TimingRunsFromTheFirstSnowToTheLastAndLeavesOutFlurries()
    {
        SnowPeriod[] periods =
        [
            new(Midnight.AddHours(2), Midnight.AddHours(3), 0.005, 0.005),
            new(Midnight.AddHours(10), Midnight.AddHours(11), 0.2, 0.2),
            new(Midnight.AddHours(11), Midnight.AddHours(12), 0, 0),
            new(Midnight.AddHours(20), Midnight.AddHours(21), 0.1, 0.1),
        ];

        Assert.Equal((Midnight.AddHours(10), Midnight.AddHours(21)), SnowWindow.Timing(periods, Midnight, Midnight.AddHours(24)));
    }

    [Fact]
    public void TimingStaysInsideTheWindow()
    {
        SnowPeriod[] periods =
        [
            new(Midnight.AddHours(-6), Midnight.AddHours(6), 0, 4),
            new(Midnight.AddHours(18), Midnight.AddHours(30), 1, 3),
            new(Midnight.AddHours(40), Midnight.AddHours(41), 1, 1),
        ];

        Assert.Equal((Midnight, Midnight.AddHours(24)), SnowWindow.Timing(periods, Midnight, Midnight.AddHours(24)));
    }

    [Fact]
    public void NoSnowHasNoTiming()
    {
        SnowPeriod[] periods = [new(Midnight, Midnight.AddHours(12), 0, 0), new(Midnight.AddHours(30), Midnight.AddHours(31), 1, 1)];

        Assert.Null(SnowWindow.Timing(periods, Midnight, Midnight.AddHours(24)));
        Assert.Null(SnowWindow.Timing([], Midnight, Midnight.AddHours(24)));
    }

    [Fact]
    public void ClipCutsPeriodsAtTheWindowAndSharesTheirAmounts()
    {
        SnowPeriod[] periods =
        [
            new(Midnight.AddHours(18), Midnight.AddHours(30), 2, 3),
            new(Midnight.AddHours(-6), Midnight.AddHours(6), 1, 2),
            new(Midnight.AddHours(40), Midnight.AddHours(41), 1, 1),
        ];

        var clipped = SnowWindow.Clip(periods, Midnight, Midnight.AddHours(24));

        Assert.Equal(
            [
                new SnowPeriod(Midnight, Midnight.AddHours(6), 0.5, 1),
                new SnowPeriod(Midnight.AddHours(18), Midnight.AddHours(24), 1, 1.5),
            ],
            clipped);
    }

    [Fact]
    public void PeakIsTheHeaviestRateAndLeavesOutFlurries()
    {
        SnowPeriod[] periods =
        [
            new(Midnight.AddHours(-1), Midnight.AddHours(1), 0, 3),
            new(Midnight.AddHours(6), Midnight.AddHours(18), 2, 6),
            new(Midnight.AddHours(20), Midnight.AddHours(21), 0.4, 0.8),
        ];

        Assert.Equal((Midnight, Midnight.AddHours(1), 1.5), SnowWindow.Peak(periods, Midnight, Midnight.AddHours(24)));
        Assert.Equal((Midnight.AddHours(20), Midnight.AddHours(21), 0.8), SnowWindow.Peak(periods, Midnight.AddHours(2), Midnight.AddHours(24)));
        Assert.Null(SnowWindow.Peak([new(Midnight, Midnight.AddHours(12), 0, 0.05)], Midnight, Midnight.AddHours(24)));
    }

    [Fact]
    public async Task CellsCarryTheSpanTotalAndPeak()
    {
        var service = Service(
        [
            new FakeSnowSource("storm", () => SnowResult.Ok(
                [
                    new SnowPeriod(Midnight.AddHours(1), Midnight.AddHours(2), 0.001, 0.001),
                    new SnowPeriod(Midnight.AddHours(10), Midnight.AddHours(11), 1, 1.5),
                    new SnowPeriod(Midnight.AddHours(11), Midnight.AddHours(12), 0.4, 0.5),
                ],
                TestData.Now)),
        ]);
        var (request, _) = service.Parse(null, "2026-10-30", "2026-10-30");

        var cell = Assert.Single((await service.GetAsync(request!)).Cells);

        Assert.Equal(1.4, cell.TimingLowInches);
        Assert.Equal(2, cell.TimingHighInches);
        Assert.Equal(Midnight.AddHours(10), cell.PeakStart);
        Assert.Equal(10, cell.PeakStart!.Value.Hour);
        Assert.Equal(Midnight.AddHours(11), cell.PeakEnd);
        Assert.Equal(1.5, cell.PeakInchesPerHour);
    }

    [Fact]
    public async Task HourlyGivesEverySourcesPeriodsInTheTownsTime()
    {
        var calls = 0;
        var service = Service(
        [
            new FakeSnowSource("good", () =>
            {
                calls++;
                return SnowResult.Ok(
                    [new SnowPeriod(Midnight.AddHours(-2), Midnight.AddHours(2), 1, 2), new SnowPeriod(Midnight.AddHours(30), Midnight.AddHours(31), 1, 1)],
                    TestData.Now,
                    [new SnowPeriod(Midnight, Midnight.AddHours(6), 0.1, 0.1)]);
            }),
            new FakeSnowSource("bad", () => throw new HttpRequestException("Down", null, HttpStatusCode.ServiceUnavailable)),
        ]);
        var (request, _) = service.ParseHourly("04086", "2026-10-30", "2026-10-30");
        var (tableRequest, _) = service.Parse("04086", "2026-10-30", "2026-10-30");
        await service.GetAsync(tableRequest!);

        var hourly = await service.GetHourlyAsync(request!);

        Assert.Equal(1, calls);
        Assert.Equal("Topsham", hourly.Name);
        Assert.Equal(TimeSpan.FromHours(-4), hourly.Now.Offset);
        var good = hourly.Sources[0];
        var period = Assert.Single(good.Periods);
        Assert.Equal(new SnowPeriod(Midnight, Midnight.AddHours(2), 0.5, 1), period);
        Assert.Equal(0, period.Start.Hour);
        Assert.Equal(0.1, Assert.Single(good.Ice!).HighInches);
        Assert.False(good.Partial);
        Assert.Equal(SourceStatus.Error, hourly.Sources[1].Status);
        Assert.Empty(hourly.Sources[1].Periods);
    }

    [Fact]
    public async Task HourlyMarksASourceThatStartsLateAsPartial()
    {
        var service = Service(
        [
            new FakeSnowSource("late", () => SnowResult.Ok([new SnowPeriod(Midnight.AddHours(12), Midnight.AddHours(36), 1, 2)], TestData.Now)),
        ]);
        var (request, _) = service.ParseHourly("04086", "2026-10-30", "2026-10-30");

        var hourly = await service.GetHourlyAsync(request!);

        Assert.True(Assert.Single(hourly.Sources).Partial);
    }

    [Fact]
    public async Task HourlyNamesAnUnknownTown()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = Service([new FakeSnowSource("good", () => SnowResult.Ok([], TestData.Now))], http);
        var (request, _) = service.ParseHourly("99999", null, null);

        var hourly = await service.GetHourlyAsync(request!);

        Assert.Equal("That ZIP code wasn't found.", hourly.Message);
        Assert.Empty(hourly.Sources);
    }

    [Theory]
    [InlineData("04086", null)]
    [InlineData(null, "Enter a 5-digit ZIP code.")]
    [InlineData("04086,04011", "Enter a 5-digit ZIP code.")]
    [InlineData("0408", "Enter a 5-digit ZIP code.")]
    public void HourlyTakesOneTown(string? zip, string? error)
    {
        var (request, message) = Service([]).ParseHourly(zip, null, null);

        Assert.Equal(error, message);
        Assert.Equal(error is null, request is not null);
    }

    [Fact]
    public async Task CellsCarryTimingInTheTownsTimeAndIceToHundredths()
    {
        var service = Service(
        [
            new FakeSnowSource("icy", () => SnowResult.Ok(
                [new SnowPeriod(Midnight.AddHours(15), Midnight.AddHours(16), 1, 1)],
                TestData.Now,
                [new SnowPeriod(Midnight, Midnight.AddHours(6), 0.127, 0.127), new SnowPeriod(Midnight.AddHours(6), Midnight.AddHours(12), 0.127, 0.127)])),
            new FakeSnowSource("plain", () => SnowResult.Ok([], TestData.Now)),
        ]);
        var (request, _) = service.Parse(null, "2026-10-30", "2026-10-30");

        var table = await service.GetAsync(request!);

        var icy = table.Cells[0];
        Assert.Equal(Midnight.AddHours(15), icy.SnowStart);
        Assert.Equal(TimeSpan.FromHours(-4), icy.SnowStart!.Value.Offset);
        Assert.Equal(15, icy.SnowStart.Value.Hour);
        Assert.Equal(Midnight.AddHours(16), icy.SnowEnd);
        Assert.Equal(0.25, icy.IceInches);

        var plain = table.Cells[1];
        Assert.Null(plain.SnowStart);
        Assert.Null(plain.IceInches);
    }

    [Fact]
    public async Task NwsReadsIceFromTheGrid()
    {
        var http = StubHttp.Json(url => url switch
        {
            _ when url.Contains("/points/") => """
                { "properties": { "forecast": "https://api.weather.gov/gridpoints/GYX/1,2/forecast", "forecastGridData": "https://api.weather.gov/gridpoints/GYX/1,2" } }
                """,
            _ when url.EndsWith("/forecast") => """{ "properties": { "periods": [] } }""",
            _ => """
                { "properties": {
                  "snowfallAmount": { "values": [] },
                  "iceAccumulation": { "uom": "wmoUnit:mm", "values": [
                    { "validTime": "2026-10-30T12:00:00+00:00/PT6H", "value": 2.54 },
                    { "validTime": "2026-10-30T18:00:00+00:00/PT6H", "value": null }
                  ] }
                } }
                """,
        });

        var result = await new NwsSnowSource(http, new WeatherCache(), TestData.Options(), Time).GetSnowAsync(TestData.Topsham, default);

        var ice = Assert.Single(result.Ice!);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T12:00:00Z"), ice.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T18:00:00Z"), ice.End);
        Assert.Equal(0.1, ice.HighInches, 6);
    }

    // Topsham is at 43.9281, -69.9656.
    private const string StormReports = """
        { "type": "FeatureCollection", "features": [
          { "properties": { "type": "S", "magf": 3.5, "unit": "Inch", "city": "Topsham", "source": "Public", "qualifier": "M", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
          { "properties": { "type": "S", "magf": 5.0, "unit": "Inch", "city": "Topsham", "source": "Public", "qualifier": "M", "valid": "2026-10-30T15:00:00Z", "lat": 43.93, "lon": -69.96 } },
          { "properties": { "type": "S", "magf": 2.0, "unit": "Inch", "city": "3 N Bowdoinham", "source": "Trained Spotter", "qualifier": "E", "valid": "2026-10-30T13:00:00Z", "lat": 44.02, "lon": -69.90 } },
          { "properties": { "type": "S", "magf": 9.0, "unit": "Inch", "city": "Topsham", "source": "Public", "qualifier": "M", "valid": "2026-10-29T15:00:00Z", "lat": 43.93, "lon": -69.96 } },
          { "properties": { "type": "S", "magf": 12.0, "unit": "Inch", "city": "1 NNE Enfield", "source": "Trained Spotter", "qualifier": "M", "valid": "2026-10-30T14:00:00Z", "lat": 45.26, "lon": -68.56 } },
          { "properties": { "type": "N", "magf": 45, "unit": "MPH", "city": "Topsham", "source": "Public", "qualifier": "M", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
          { "properties": { "type": "S", "magf": null, "unit": "Inch", "city": "Topsham", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } }
        ] }
        """;

    private static (SnowReportService Reports, SnowTableService Tables) ReportServices(StubHttp http)
    {
        var cache = new WeatherCache();
        var options = TestData.Options();
        var locations = new LocationResolver(http, cache, options);
        return (
            new SnowReportService(http, locations, cache, options, Time, NullLogger<SnowReportService>.Instance),
            new SnowTableService([], locations, cache, options, Time, NullLogger<SnowTableService>.Instance));
    }

    [Fact]
    public async Task ReportsNearATownGiveItsLowestAndHighestTotals()
    {
        var http = StubHttp.Json(_ => StormReports);
        var (reports, tables) = ReportServices(http);
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-30");

        var table = await reports.GetAsync(request!);

        Assert.Equal(SourceStatus.Ok, table.Status);
        var town = Assert.Single(table.Towns);
        Assert.Equal("04086", town.Zip);
        Assert.Equal(2.0, town.Low);
        Assert.Equal(5.0, town.High);
        Assert.Equal(["Topsham", "3 N Bowdoinham"], town.Reports.Select(report => report.Place));

        // A place's highest report is its total so far, and times are the town's.
        Assert.Equal(5.0, town.Reports[0].Amount);
        Assert.Equal(11, town.Reports[0].ObservedAt.Hour);
        Assert.False(town.Reports[0].Estimated);
        Assert.True(town.Reports[1].Estimated);
        Assert.InRange(town.Reports[1].DistanceMiles, 6, 8);

        var url = Assert.Single(http.Requests).RequestUri!.ToString();
        // The same call covers the past week's rain, from local midnight 7 days ago.
        Assert.Contains("sts=202610230400", url);
        Assert.Contains("ets=202610311600", url);
        Assert.Contains("states=ME", url);
    }

    [Fact]
    public async Task ReportsNeverGiveCoordinates()
    {
        var (reports, tables) = ReportServices(StubHttp.Json(_ => StormReports));
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-30");

        var json = System.Text.Json.JsonSerializer.Serialize(await reports.GetAsync(request!));

        Assert.DoesNotContain("43.93", json);
        Assert.DoesNotContain("69.96", json);
    }

    [Fact]
    public async Task DatesThatHaveNotStartedStillGetThePastWeeksRain()
    {
        var http = StubHttp.Json(_ => """
            { "features": [
              { "properties": { "type": "S", "magf": 3.0, "unit": "Inch", "city": "Topsham", "valid": "2026-10-29T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 1.1, "unit": "Inch", "city": "Topsham", "valid": "2026-10-29T14:00:00Z", "lat": 43.93, "lon": -69.96 } }
            ] }
            """);
        var (reports, tables) = ReportServices(http);
        var (request, _) = tables.Parse("04086", "2026-11-02", "2026-11-03");

        var table = await reports.GetAsync(request!);

        // Snow and gusts wait for the storm's dates; rain is what has already fallen.
        Assert.Equal(SourceStatus.Ok, table.Status);
        Assert.Empty(table.Towns);
        Assert.Empty(table.WindTowns!);
        Assert.Equal(1.1, Assert.Single(table.RainTowns!).High);
        Assert.Equal(new DateOnly(2026, 10, 23), table.RainSince);
        var url = Assert.Single(http.Requests).RequestUri!.ToString();
        Assert.Contains("sts=202610230400", url);
        // Nothing can be reported after today, so a storm still to come doesn't stretch the call.
        Assert.Contains("ets=202610310400", url);
    }

    [Fact]
    public async Task RainReportsCoverThePastWeekWithEachPlacesDatesNewestFirst()
    {
        var http = StubHttp.Json(_ => """
            { "features": [
              { "properties": { "type": "R", "magf": 0.5, "unit": "Inch", "city": "Topsham", "valid": "2026-10-25T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 0.8, "unit": "Inch", "city": "Topsham", "valid": "2026-10-25T20:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 1.2, "unit": "Inch", "city": "Topsham", "valid": "2026-10-29T12:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 2.0, "unit": "Inch", "city": "Brunswick", "valid": "2026-10-29T13:00:00Z", "lat": 43.91, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 3.0, "unit": "Inch", "city": "Topsham", "valid": "2026-10-21T12:00:00Z", "lat": 43.93, "lon": -69.96 } }
            ] }
            """);
        var (reports, tables) = ReportServices(http);
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-31");

        var town = Assert.Single((await reports.GetAsync(request!)).RainTowns!);

        // A place keeps its highest report on each date; one from before the week is left out.
        Assert.Equal(
            [("Topsham", 1.2), ("Brunswick", 2.0), ("Topsham", 0.8)],
            town.Reports.Select(report => (report.Place, report.Amount)));
    }

    [Fact]
    public async Task ATownWithNoReportsNearbyHasNoTotals()
    {
        var (reports, tables) = ReportServices(StubHttp.Json(_ => """{ "features": [] }"""));
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-30");

        var town = Assert.Single((await reports.GetAsync(request!)).Towns);

        Assert.Null(town.Low);
        Assert.Null(town.High);
        Assert.Empty(town.Reports);
    }

    [Fact]
    public async Task FailedReportsSaySoWithoutThrowing()
    {
        var (reports, tables) = ReportServices(new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-30");

        var table = await reports.GetAsync(request!);

        Assert.Equal(SourceStatus.Error, table.Status);
        Assert.Equal("Storm reports are unavailable for some towns right now.", table.Message);
        Assert.Empty(table.Towns);
    }

    [Fact]
    public async Task ReportsSplitIntoSnowRainAndWind()
    {
        var http = StubHttp.Json(_ => """
            { "features": [
              { "properties": { "type": "S", "magf": 4.0, "unit": "Inch", "city": "Topsham", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "R", "magf": 1.25, "unit": "Inch", "city": "Topsham", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "N", "magf": 45, "unit": "MPH", "city": "Topsham", "valid": "2026-10-30T14:00:00Z", "lat": 43.93, "lon": -69.96 } },
              { "properties": { "type": "G", "magf": 52, "unit": "MPH", "city": "Brunswick", "valid": "2026-10-30T15:00:00Z", "lat": 43.91, "lon": -69.96 } },
              { "properties": { "type": "M", "magf": 60, "unit": "MPH", "city": "Casco Bay", "valid": "2026-10-30T15:00:00Z", "lat": 43.80, "lon": -70.00 } },
              { "properties": { "type": "E", "magf": null, "unit": null, "city": "Topsham", "valid": "2026-10-30T15:00:00Z", "lat": 43.93, "lon": -69.96 } }
            ] }
            """);
        var (reports, tables) = ReportServices(http);
        var (request, _) = tables.Parse("04086", "2026-10-30", "2026-10-30");

        var table = await reports.GetAsync(request!);

        Assert.Equal(4.0, Assert.Single(table.Towns).High);
        Assert.Equal(1.25, Assert.Single(table.RainTowns!).High);
        var wind = Assert.Single(table.WindTowns!);
        Assert.Equal(45, wind.Low);
        Assert.Equal(52, wind.High);
        Assert.Equal(["Topsham", "Brunswick"], wind.Reports.Select(report => report.Place));
        Assert.Single(http.Requests);
    }

    [Theory]
    [InlineData("Winter Storm Warning", StormAlertKind.Winter)]
    [InlineData("Wind Chill Advisory", StormAlertKind.Winter)]
    [InlineData("Flood Watch", StormAlertKind.Flood)]
    [InlineData("Flash Flood Warning", StormAlertKind.Flood)]
    [InlineData("Coastal Flood Advisory", StormAlertKind.Flood)]
    [InlineData("High Wind Warning", StormAlertKind.Wind)]
    [InlineData("Wind Advisory", StormAlertKind.Wind)]
    [InlineData("Tropical Storm Watch", StormAlertKind.Wind)]
    [InlineData("Small Craft Advisory", null)]
    [InlineData("Dense Fog Advisory", null)]
    public void AlertsBelongToAView(string eventName, StormAlertKind? kind)
    {
        Assert.Equal(kind, SnowAlertService.Kind(eventName));
    }

    [Fact]
    public async Task FloodAndWindAlertsComeFromTheSameCall()
    {
        var cache = new WeatherCache();
        var options = TestData.Options();
        var http = StubHttp.Json(url => url.Contains("zippopotam")
            ? """{ "places": [ { "place name": "Topsham", "state abbreviation": "ME", "latitude": "43.93", "longitude": "-69.97" } ] }"""
            : url.Contains("timezone=auto")
                ? """{ "timezone": "America/New_York" }"""
                : """
                  { "features": [
                    { "properties": { "id": "a1", "event": "Flood Watch", "severity": "Moderate" } },
                    { "properties": { "id": "a2", "event": "Wind Advisory", "severity": "Minor" } },
                    { "properties": { "id": "a3", "event": "Small Craft Advisory", "severity": "Minor" } }
                  ] }
                  """);
        var service = new SnowAlertService(
            new LocationResolver(http, cache, options), new NwsAlerts(http, Time), cache, options, NullLogger<SnowAlertService>.Instance);

        var report = await service.GetAsync(["04086"]);

        Assert.Equal([StormAlertKind.Flood, StormAlertKind.Wind], report.Alerts.Select(alert => alert.Kind));
    }

    [Fact]
    public async Task OpenMeteoReadsLiquidGustsAndDepthFromTheSameCall()
    {
        var http = StubHttp.Json(_ => """
            { "hourly_units": { "snow_depth_gfs_seamless": "ft", "snow_depth_jma_seamless": "ft" },
              "hourly": {
              "time": ["2026-10-30T05:00", "2026-10-30T06:00"],
              "snowfall_gfs_seamless": [0.4, 0.2],
              "precipitation_gfs_seamless": [0.04, 0.03],
              "wind_gusts_10m_gfs_seamless": [32.5, 41],
              "snow_depth_gfs_seamless": [0.5, 0.25],
              "snowfall_jma_seamless": [0, 0],
              "precipitation_jma_seamless": [0.1, null],
              "wind_gusts_10m_jma_seamless": [null, null],
              "snow_depth_jma_seamless": [null, null]
            } }
            """);
        string[] models = ["gfs_seamless", "jma_seamless"];
        var cache = new WeatherCache();
        OpenMeteoSnowSource Source(string model) =>
            new(new ColumnDescriptor(model, model), model, "https://api.open-meteo.com/v1/forecast", models, http, cache, TestData.Options(), Time);

        var gfs = await Source("gfs_seamless").GetSnowAsync(TestData.Topsham, default);
        var jma = await Source("jma_seamless").GetSnowAsync(TestData.Topsham, default);

        Assert.Equal(
            [new SnowPeriod(Midnight, Midnight.AddHours(1), 0.04, 0.04), new SnowPeriod(Midnight.AddHours(1), Midnight.AddHours(2), 0.03, 0.03)],
            gfs.Liquid);
        Assert.Equal([new GustPeriod(Midnight, Midnight.AddHours(1), 32.5), new GustPeriod(Midnight.AddHours(1), Midnight.AddHours(2), 41)], gfs.Gusts);

        // Depth comes in feet with inch units, at the hour's time.
        Assert.Equal([new DepthPoint(Midnight.AddHours(1), 6), new DepthPoint(Midnight.AddHours(2), 3)], gfs.Depth);

        // A model without gusts or depth has none, rather than an empty series.
        Assert.Null(jma.Gusts);
        Assert.Null(jma.Depth);
        Assert.Single(jma.Liquid!);

        var query = Assert.Single(http.Requests).RequestUri!.Query;
        Assert.Contains("hourly=snowfall,precipitation,wind_gusts_10m,snow_depth", query);
        Assert.Contains("wind_speed_unit=mph", query);
    }

    [Fact]
    public async Task NwsReadsLiquidAndGustsFromTheGrid()
    {
        var http = StubHttp.Json(url => url switch
        {
            _ when url.Contains("/points/") => """
                { "properties": { "forecast": "https://api.weather.gov/gridpoints/GYX/1,2/forecast", "forecastGridData": "https://api.weather.gov/gridpoints/GYX/1,2" } }
                """,
            _ when url.EndsWith("/forecast") => """{ "properties": { "periods": [] } }""",
            _ => """
                { "properties": {
                  "snowfallAmount": { "values": [] },
                  "quantitativePrecipitation": { "uom": "wmoUnit:mm", "values": [
                    { "validTime": "2026-10-30T12:00:00+00:00/PT6H", "value": 12.7 }
                  ] },
                  "windGust": { "uom": "wmoUnit:km_h-1", "values": [
                    { "validTime": "2026-10-30T12:00:00+00:00/PT2H", "value": 64.374 },
                    { "validTime": "2026-10-30T14:00:00+00:00/PT1H", "value": null }
                  ] }
                } }
                """,
        });

        var result = await new NwsSnowSource(http, new WeatherCache(), TestData.Options(), Time).GetSnowAsync(TestData.Topsham, default);

        var liquid = Assert.Single(result.Liquid!);
        Assert.Equal(0.5, liquid.HighInches, 6);
        Assert.Equal(TimeSpan.FromHours(6), liquid.End - liquid.Start);
        var gust = Assert.Single(result.Gusts!);
        Assert.Equal(40, gust.Mph);
        Assert.Equal(TimeSpan.FromHours(2), gust.End - gust.Start);
        Assert.Null(result.Depth);
    }

    [Fact]
    public async Task WeatherUndergroundReadsLiquidButHasNoGusts()
    {
        var http = StubHttp.Json(_ => """
            {
              "validTimeLocal": ["2026-10-30T07:00:00-0400"],
              "daypart": [{
                "qpf": [null, 0.45],
                "qpfSnow": [null, 2.4],
                "narrative": [null, "Snow."]
              }]
            }
            """);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "key");

        var result = await new WeatherUndergroundSnowSource(http, new WeatherCache(), options, Time).GetSnowAsync(TestData.Topsham, default);

        var dayStart = DateTimeOffset.Parse("2026-10-30T07:00:00-04:00");
        Assert.Equal([new SnowPeriod(dayStart.AddHours(12), dayStart.AddHours(24), 0.45, 0.45)], result.Liquid);
        Assert.Null(result.Gusts);
    }

    [Fact]
    public async Task WeatherApiReadsLiquidAndGusts()
    {
        var start = Midnight.ToUnixTimeSeconds();
        var http = StubHttp.Json(_ => $$"""
            { "forecast": { "forecastday": [ { "hour": [
              { "time_epoch": {{start}}, "snow_cm": 0, "precip_in": 0.12, "gust_mph": 38.2 }
            ] } ] } }
            """);
        var options = TestData.Options(o => o.WeatherApi.ApiKey = "key");

        var result = await new WeatherApiSnowSource(http, options, Time).GetSnowAsync(TestData.Topsham, default);

        Assert.Equal([new SnowPeriod(Midnight, Midnight.AddHours(1), 0.12, 0.12)], result.Liquid);
        Assert.Equal([new GustPeriod(Midnight, Midnight.AddHours(1), 38.2)], result.Gusts);
    }

    [Fact]
    public async Task TableBuildsRainWindAndDepthCellsFromOneResult()
    {
        var calls = 0;
        var service = Service(
        [
            new FakeSnowSource("model", () =>
            {
                calls++;
                return SnowResult.Ok(
                    [
                        new SnowPeriod(Midnight, Midnight.AddHours(6), 0, 0),
                        new SnowPeriod(Midnight.AddHours(6), Midnight.AddHours(12), 6, 6),
                        new SnowPeriod(Midnight.AddHours(12), Midnight.AddHours(24), 0, 0),
                    ],
                    TestData.Now,
                    liquid: [new SnowPeriod(Midnight, Midnight.AddHours(24), 0.604, 0.604)],
                    gusts:
                    [
                        new GustPeriod(Midnight.AddHours(8), Midnight.AddHours(9), 31),
                        new GustPeriod(Midnight.AddHours(12), Midnight.AddHours(13), 44.6),
                        new GustPeriod(Midnight.AddHours(20), Midnight.AddHours(21), 25),
                        new GustPeriod(Midnight.AddHours(23), Midnight.AddHours(24), 12),
                    ],
                    depth: [new DepthPoint(Midnight.AddHours(6), 0), new DepthPoint(Midnight.AddHours(14), 5.5), new DepthPoint(Midnight.AddHours(24), 4)]);
            }),
            new FakeSnowSource("plain", () => SnowResult.Ok([new SnowPeriod(Midnight, Midnight.AddHours(24), 1, 1)], TestData.Now)),
        ]);
        var (request, _) = service.Parse(null, "2026-10-30", "2026-10-30");

        var table = await service.GetAsync(request!);

        Assert.Equal(1, calls);
        var rain = table.RainCells![0];
        Assert.Equal(0.6, rain.HighInches);
        Assert.Null(rain.IceInches);
        Assert.Equal(10, rain.Ratio);
        Assert.False(rain.Mixed);
        Assert.Equal(10, table.Cells[0].Ratio);

        var wind = table.WindCells![0];
        Assert.Equal(45, wind.MaxMph);
        Assert.Equal(Midnight.AddHours(12), wind.PeakStart);
        Assert.Equal(Midnight.AddHours(8), wind.WindyStart);
        Assert.Equal(Midnight.AddHours(13), wind.WindyEnd);
        Assert.Equal(TimeSpan.FromHours(-4), wind.PeakStart!.Value.Offset);

        var depth = table.DepthCells![0];
        Assert.Equal(5.5, depth.MaxInches);
        Assert.Equal(Midnight.AddHours(14), depth.MaxAt);
        Assert.Equal(4, depth.EndInches);
        Assert.False(depth.Partial);

        // A source without liquid, gusts or depth has no values there, and its row is only in snow and rain.
        Assert.Null(table.RainCells[1].HighInches);
        Assert.Null(table.WindCells[1].MaxMph);
        Assert.Equal([StormViews.Snow, StormViews.Rain, StormViews.Wind, StormViews.Depth], table.Sources[0].Views);
        Assert.Equal([StormViews.Snow, StormViews.Rain], table.Sources[1].Views);
    }

    [Theory]
    [InlineData(2, 1, 2.0, true)]
    [InlineData(8, 1, 8.0, false)]
    [InlineData(0, 1, null, false)]
    [InlineData(1, 0.02, null, false)]
    public void RatioAndMixed(double snow, double liquid, double? ratio, bool mixed)
    {
        Assert.Equal((ratio, mixed), SnowWindow.Ratio((snow, snow, false), (liquid, liquid, false)));
    }

    [Fact]
    public void NoRatioWhenEitherTotalIsPartial()
    {
        Assert.Equal((null, false), SnowWindow.Ratio((5, 5, true), (0.5, 0.5, false)));
        Assert.Equal((null, false), SnowWindow.Ratio((5, 5, false), (0.5, 0.5, true)));
    }

    [Fact]
    public void MaxGustIsPartialWhenTheForecastEndsFirst()
    {
        GustPeriod[] gusts = [new(Midnight, Midnight.AddHours(12), 40)];

        var max = SnowWindow.MaxGust(gusts, Midnight, Midnight.AddHours(24));

        Assert.Equal((40.0, Midnight, Midnight.AddHours(12), true), max);
        Assert.Null(SnowWindow.MaxGust(gusts, Midnight.AddHours(12), Midnight.AddHours(24)));
    }

    [Fact]
    public void DepthNowIsTheNearestPointWithinNinetyMinutes()
    {
        DepthPoint[] depth = [new(Midnight, 2), new(Midnight.AddHours(1), 3), new(Midnight.AddHours(2), 3)];

        Assert.Equal(3, SnowWindow.Depth(depth, Midnight.AddMinutes(50), Midnight, Midnight.AddHours(24))!.Value.Now);
        Assert.Null(SnowWindow.Depth(depth, Midnight.AddHours(10), Midnight, Midnight.AddHours(24))!.Value.Now);
        Assert.True(SnowWindow.Depth(depth, Midnight, Midnight, Midnight.AddHours(24))!.Value.Partial);
    }

    [Fact]
    public async Task HourlyCarriesLiquidGustsAndDepthInTheTownsTime()
    {
        var service = Service(
        [
            new FakeSnowSource("model", () => SnowResult.Ok(
                [],
                TestData.Now,
                liquid: [new SnowPeriod(Midnight.AddHours(-2), Midnight.AddHours(2), 0.4, 0.4)],
                gusts: [new GustPeriod(Midnight.AddHours(-1), Midnight.AddHours(1), 35)],
                depth: [new DepthPoint(Midnight.AddHours(-1), 1), new DepthPoint(Midnight.AddHours(3), 2)])),
        ]);
        var (request, _) = service.ParseHourly("04086", "2026-10-30", "2026-10-30");

        var source = Assert.Single((await service.GetHourlyAsync(request!)).Sources);

        Assert.Equal(new SnowPeriod(Midnight, Midnight.AddHours(2), 0.2, 0.2), Assert.Single(source.Liquid!));
        Assert.True(source.LiquidPartial);
        var gust = Assert.Single(source.Gusts!);
        Assert.Equal(new GustPeriod(Midnight, Midnight.AddHours(1), 35), gust);
        Assert.Equal(0, gust.Start.Hour);
        Assert.Equal(new DepthPoint(Midnight.AddHours(3), 2), Assert.Single(source.Depth!));
    }
}
