using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class SourceTests
{
    private static readonly FixedTime Time = new(TestData.Now);

    [Fact]
    public async Task OpenMeteoRoundsHighsAndLowsAndSkipsNullDays()
    {
        var http = StubHttp.Json(_ => """
            {"daily":{"time":["2026-10-30","2026-10-31","2026-11-01"],"temperature_2m_max":[64.5,47.4,null],"temperature_2m_min":[40.4,null,null]}}
            """);
        var source = new OpenMeteoSource(new ColumnDescriptor("ecmwf", "ECMWF"), "ecmwf_ifs025", OpenMeteoSource.ForecastEndpoint, http, TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 30), 65, 40), new DailyTemperatures(new DateOnly(2026, 10, 31), 47, null)],
            result.Days);
        Assert.Contains("models=ecmwf_ifs025", http.Requests[0].RequestUri!.Query);
        Assert.Contains("daily=temperature_2m_max,temperature_2m_min", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task OpenMeteoEnsembleMeanUsesTheEnsembleEndpoint()
    {
        var http = StubHttp.Json(_ => """
            {"daily":{"time":["2026-10-30","2026-10-31"],"temperature_2m_max":[64.2,null],"temperature_2m_min":[41.6,null]}}
            """);
        var source = new OpenMeteoSource(
            new ColumnDescriptor("weathernext", "WeatherNext (mean)"),
            "google_weathernext2_ensemble_mean",
            OpenMeteoSource.EnsembleEndpoint,
            http,
            TestData.Options(),
            Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal([new DailyTemperatures(new DateOnly(2026, 10, 30), 64, 42)], result.Days);
        Assert.StartsWith("https://ensemble-api.open-meteo.com/v1/ensemble?", http.Requests[0].RequestUri!.ToString());
        Assert.Contains("models=google_weathernext2_ensemble_mean", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task OpenMeteoKeepsHighsWhenLowsAreMissing()
    {
        var http = StubHttp.Json(_ => """
            {"daily":{"time":["2026-10-30"],"temperature_2m_max":[64.2]}}
            """);
        var source = new OpenMeteoSource(new ColumnDescriptor("gem", "GEM"), "gem_seamless", OpenMeteoSource.ForecastEndpoint, http, TestData.Options(), Time);

        var result = await source.GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal([new DailyTemperatures(new DateOnly(2026, 10, 30), 64, null)], result.Days);
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
    public async Task WeatherUndergroundReadsCalendarDayHighsAndLows()
    {
        var http = StubHttp.Json(_ => """
            {"validTimeLocal":["2026-10-30T07:00:00-0400","2026-10-31T07:00:00-0400","2026-11-01T07:00:00-0400"],"calendarDayTemperatureMax":[66,null,null],"calendarDayTemperatureMin":[41,39,null]}
            """);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "test-key");

        var result = await new WeatherUndergroundSource(http, options, Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 30), 66, 41), new DailyTemperatures(new DateOnly(2026, 10, 31), null, 39)],
            result.Days);
        Assert.Contains("postalKey=04086:US", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task WeatherUndergroundKeepsHighsWhenLowsAreMissing()
    {
        var http = StubHttp.Json(_ => """
            {"validTimeLocal":["2026-10-30T07:00:00-0400"],"calendarDayTemperatureMax":[66]}
            """);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "test-key");

        var result = await new WeatherUndergroundSource(http, options, Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal([new DailyTemperatures(new DateOnly(2026, 10, 30), 66, null)], result.Days);
    }

    [Fact]
    public async Task WeatherUndergroundReportsNeedsKeyWithoutCallingOut()
    {
        var http = StubHttp.Json(_ => throw new InvalidOperationException("No request expected."));

        var result = await new WeatherUndergroundSource(http, TestData.Options(), Time).GetDailyTemperaturesAsync(TestData.Topsham, default);

        Assert.Equal(SourceStatus.NeedsKey, result.Status);
        Assert.Empty(http.Requests);
    }
}
