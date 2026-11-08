using System.Globalization;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class SourceTests
{
    private static readonly FixedTime Time = new(TestData.Now);
    private static readonly DateTime HourlyStart = new(2026, 10, 29, 0, 0, 0);

    private static string Hourly(IReadOnlyList<double?> temperatures)
    {
        var times = Enumerable.Range(0, temperatures.Count).Select(hour => $"\"{HourlyStart.AddHours(hour):yyyy-MM-dd'T'HH:mm}\"");
        var values = temperatures.Select(value => value?.ToString(CultureInfo.InvariantCulture) ?? "null");
        return $$$"""{"hourly":{"time":[{{{string.Join(',', times)}}}],"temperature_2m":[{{{string.Join(',', values)}}}]}}""";
    }

    [Fact]
    public async Task OpenMeteoUsesTheAfternoonHighAndMorningLow()
    {
        // Three days of hours from midnight on Oct 29.
        var temperatures = Enumerable.Repeat<double?>(50, 72).ToArray();
        temperatures[22] = 41.4; // Oct 29, 10pm: the night before Oct 30
        temperatures[26] = 70;   // Oct 30, 2am: warm, but not the afternoon
        temperatures[38] = 64.6; // Oct 30, 2pm
        temperatures[46] = 30;   // Oct 30, 10pm: the night before Oct 31
        var http = StubHttp.Json(_ => Hourly(temperatures));
        var source = new OpenMeteoSource(new ColumnDescriptor("ecmwf", "ECMWF"), "ecmwf_ifs025", OpenMeteoSource.ForecastEndpoint, ["ecmwf_ifs025"], http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        // Oct 29 has no low because its evening before isn't in the response.
        Assert.Equal(
            [
                new DailyTemperatures(new DateOnly(2026, 10, 29), 50, null),
                new DailyTemperatures(new DateOnly(2026, 10, 30), 65, 41),
                new DailyTemperatures(new DateOnly(2026, 10, 31), 50, 30),
            ],
            result.Days);
        Assert.Contains("models=ecmwf_ifs025", http.Requests[0].RequestUri!.Query);
        Assert.Contains("hourly=temperature_2m", http.Requests[0].RequestUri!.Query);
        Assert.Contains("past_days=1", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task OpenMeteoModelsShareOneCallAndReadTheirOwnSeries()
    {
        var times = string.Join(',', Enumerable.Range(0, 48).Select(hour => $"\"{HourlyStart.AddHours(hour):yyyy-MM-dd'T'HH:mm}\""));
        var http = StubHttp.Json(_ => $$$"""
            {"hourly":{"time":[{{{times}}}],
              "temperature_2m_gfs_seamless":[{{{string.Join(',', Enumerable.Repeat(40, 48))}}}],
              "temperature_2m_gem_seamless":[{{{string.Join(',', Enumerable.Repeat(60, 48))}}}]}}
            """);
        string[] models = ["gfs_seamless", "gem_seamless"];
        var cache = new WeatherCache();
        OpenMeteoSource Source(string model) =>
            new(new ColumnDescriptor(model, model), model, OpenMeteoSource.ForecastEndpoint, models, http, cache, TestData.Options(), Time);

        var gfs = await Source("gfs_seamless").GetDailyTemperaturesAsync(TestData.Topsham, default);
        var gem = await Source("gem_seamless").GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(40, gfs.Days[^1].HighF);
        Assert.Equal(60, gem.Days[^1].HighF);
        Assert.Contains("models=gfs_seamless,gem_seamless", Assert.Single(http.Requests).RequestUri!.Query);
    }

    [Fact]
    public async Task OpenMeteoLeavesOutAWindowTheModelDoesNotCover()
    {
        // The model ends at noon on Oct 30, partway through that afternoon.
        var temperatures = Enumerable.Range(0, 48).Select(hour => hour < 36 ? (double?)50 : null).ToArray();
        var http = StubHttp.Json(_ => Hourly(temperatures));
        var source = new OpenMeteoSource(new ColumnDescriptor("gem", "GEM"), "gem_seamless", OpenMeteoSource.ForecastEndpoint, ["gem_seamless"], http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 29), 50, null), new DailyTemperatures(new DateOnly(2026, 10, 30), null, 50)],
            result.Days);
    }

    [Fact]
    public async Task OpenMeteoEnsembleMeanUsesTheEnsembleEndpoint()
    {
        var http = StubHttp.Json(_ => Hourly(Enumerable.Repeat<double?>(50, 48).ToArray()));
        var source = new OpenMeteoSource(
            SourceColumns.WeatherNext,
            "google_weathernext2_ensemble_mean",
            OpenMeteoSource.EnsembleEndpoint,
            ["google_weathernext2_ensemble_mean"],
            http,
            new WeatherCache(),
            TestData.Options(),
            Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(new DailyTemperatures(new DateOnly(2026, 10, 30), 50, 50), result.Days[^1]);
        Assert.StartsWith("https://ensemble-api.open-meteo.com/v1/ensemble?", http.Requests[0].RequestUri!.ToString());
        Assert.Contains("models=google_weathernext2_ensemble_mean", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task NwsPutsEachNightsLowOnTheMorningItEnds()
    {
        var http = StubHttp.Json(url => url.Contains("/points/")
            ? """{"properties":{"forecast":"https://api.weather.gov/gridpoints/GYX/80,72/forecast"}}"""
            : """
            {"properties":{"periods":[
              {"isDaytime":false,"startTime":"2026-10-30T18:00:00-04:00","temperature":38},
              {"isDaytime":true,"startTime":"2026-10-31T06:00:00-04:00","temperature":52},
              {"isDaytime":false,"startTime":"2026-10-31T18:00:00-04:00","temperature":35}
            ]}}
            """);
        var source = new NwsSource(http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        // An evening forecast has no high for today, and tonight's low belongs to tomorrow.
        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 31), 52, 38), new DailyTemperatures(new DateOnly(2026, 11, 1), null, 35)],
            result.Days);
        Assert.Contains("/points/43.9281,-69.9656", http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task NwsKeepsAnOvernightPeriodAfterMidnightOnItsOwnDate()
    {
        var http = StubHttp.Json(url => url.Contains("/points/")
            ? """{"properties":{"forecast":"https://api.weather.gov/gridpoints/GYX/80,72/forecast"}}"""
            : """
            {"properties":{"periods":[
              {"isDaytime":false,"startTime":"2026-10-30T02:00:00-04:00","temperature":36},
              {"isDaytime":true,"startTime":"2026-10-30T06:00:00-04:00","temperature":55}
            ]}}
            """);
        var source = new NwsSource(http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal([new DailyTemperatures(new DateOnly(2026, 10, 30), 55, 36)], result.Days);
    }

    [Fact]
    public async Task WeatherUndergroundUsesDayPartsForTheAfternoonHighAndMorningLow()
    {
        // Today's day part is null in the evening. Each night's low is the next date's morning low.
        var http = StubHttp.Json(_ => """
            {"validTimeLocal":["2026-10-30T07:00:00-0400","2026-10-31T07:00:00-0400"],
             "calendarDayTemperatureMax":[70,66],"calendarDayTemperatureMin":[30,30],
             "daypart":[{"dayOrNight":[null,"N","D","N"],"temperature":[null,41,66,39]}]}
            """);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "test-key");

        var result = await new WeatherUndergroundSource(http, new WeatherCache(), options, Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 31), 66, 41), new DailyTemperatures(new DateOnly(2026, 11, 1), null, 39)],
            result.Days);
        Assert.Contains("postalKey=04086:US", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task WeatherUndergroundReportsNeedsKeyWithoutCallingOut()
    {
        var http = StubHttp.Json(_ => throw new InvalidOperationException("No request expected."));

        var result = await new WeatherUndergroundSource(http, new WeatherCache(), TestData.Options(), Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(SourceStatus.NeedsKey, result.Status);
        Assert.Empty(http.Requests);
    }
}
