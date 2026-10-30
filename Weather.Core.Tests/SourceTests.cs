using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class SourceTests
{
    private static readonly FixedTime Time = new(TestData.Now);

    [Fact]
    public async Task OpenMeteoRoundsHighsAndSkipsNullDays()
    {
        var http = StubHttp.Json(_ => """
            {"daily":{"time":["2026-10-30","2026-10-31","2026-11-01"],"temperature_2m_max":[64.5,47.4,null]}}
            """);
        var source = new OpenMeteoSource(new ColumnDescriptor("ecmwf", "ECMWF"), "ecmwf_ifs025", OpenMeteoSource.ForecastEndpoint, http, TestData.Options(), Time);

        var result = await source.GetDailyHighsAsync(TestData.Topsham, default);

        Assert.Equal(
            [new DailyHigh(new DateOnly(2026, 10, 30), 65), new DailyHigh(new DateOnly(2026, 10, 31), 47)],
            result.Highs);
        Assert.Contains("models=ecmwf_ifs025", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task OpenMeteoEnsembleMeanUsesTheEnsembleEndpoint()
    {
        var http = StubHttp.Json(_ => """
            {"daily":{"time":["2026-10-30","2026-10-31"],"temperature_2m_max":[64.2,null]}}
            """);
        var source = new OpenMeteoSource(
            new ColumnDescriptor("weathernext", "WeatherNext (mean)"),
            "google_weathernext2_ensemble_mean",
            OpenMeteoSource.EnsembleEndpoint,
            http,
            TestData.Options(),
            Time);

        var result = await source.GetDailyHighsAsync(TestData.Topsham, default);

        Assert.Equal([new DailyHigh(new DateOnly(2026, 10, 30), 64)], result.Highs);
        Assert.StartsWith("https://ensemble-api.open-meteo.com/v1/ensemble?", http.Requests[0].RequestUri!.ToString());
        Assert.Contains("models=google_weathernext2_ensemble_mean", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task NwsSkipsOvernightPeriodsSoAnEveningForecastStartsTomorrow()
    {
        var http = StubHttp.Json(url => url.Contains("/points/")
            ? """{"properties":{"forecast":"https://api.weather.gov/gridpoints/GYX/80,72/forecast"}}"""
            : """
            {"properties":{"periods":[
              {"isDaytime":false,"startTime":"2026-10-30T18:00:00-04:00","temperature":38},
              {"isDaytime":true,"startTime":"2026-10-31T06:00:00-04:00","temperature":52}
            ]}}
            """);
        var source = new NwsSource(http, new WeatherCache(), TestData.Options(), Time);

        var result = await source.GetDailyHighsAsync(TestData.Topsham, default);

        Assert.Equal([new DailyHigh(new DateOnly(2026, 10, 31), 52)], result.Highs);
        Assert.Contains("/points/43.9281,-69.9656", http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task WeatherUndergroundReadsCalendarDayHighs()
    {
        var http = StubHttp.Json(_ => """
            {"validTimeLocal":["2026-10-30T07:00:00-0400","2026-10-31T07:00:00-0400"],"calendarDayTemperatureMax":[66,null]}
            """);
        var options = TestData.Options(o => o.WeatherUnderground.ApiKey = "test-key");

        var result = await new WeatherUndergroundSource(http, options, Time).GetDailyHighsAsync(TestData.Topsham, default);

        Assert.Equal([new DailyHigh(new DateOnly(2026, 10, 30), 66)], result.Highs);
        Assert.Contains("postalKey=04086:US", http.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task WeatherUndergroundReportsNeedsKeyWithoutCallingOut()
    {
        var http = StubHttp.Json(_ => throw new InvalidOperationException("No request expected."));

        var result = await new WeatherUndergroundSource(http, TestData.Options(), Time).GetDailyHighsAsync(TestData.Topsham, default);

        Assert.Equal(SourceStatus.NeedsKey, result.Status);
        Assert.Empty(http.Requests);
    }
}
