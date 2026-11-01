using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class OpenMeteoStationTests
{
    private const string Id = "om-04086";

    // 2026-10-30 12:00 in Topsham is 16:00 UTC, so local midnight is 04:00 UTC.
    private static long Unix(int day, int hour) =>
        new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static string Body() => $$"""
        {
          "current": {
            "time": {{Unix(30, 16)}}, "interval": 900,
            "temperature_2m": 39.9, "apparent_temperature": 33.5, "dew_point_2m": 22.7,
            "relative_humidity_2m": 50, "wind_speed_10m": 3.1, "wind_gusts_10m": 8.7,
            "wind_direction_10m": 257, "pressure_msl": 1000.0, "precipitation": 0.01,
            "uv_index": 1.5, "shortwave_radiation": 287.0
          },
          "hourly": {
            "time": [{{Unix(29, 15)}}, {{Unix(29, 17)}}, {{Unix(30, 3)}}, {{Unix(30, 5)}}, {{Unix(30, 16)}}, {{Unix(30, 17)}}],
            "temperature_2m": [50.0, 51.0, 45.0, 44.0, 40.0, 41.0],
            "wind_speed_10m": [1.0, 2.0, 3.0, 4.0, 5.0, 6.0],
            "wind_gusts_10m": [90.0, 11.0, 80.0, 12.0, 14.0, 70.0],
            "pressure_msl": [1000.0, 1000.0, 1000.0, 1000.0, 1000.0, 1000.0],
            "precipitation": [1.0, 0.0, 1.0, 0.1, 0.2, 1.0]
          }
        }
        """;

    private static StationService Service(StubHttp http)
    {
        var options = TestData.Options();
        var time = new FixedTime(TestData.Now);
        return new StationService(
            new StationDirectory([], http, options, TestData.Locations(http, options), time),
            new WeatherCache(),
            options,
            time,
            TestData.Sky(http, options),
            NullLogger<StationService>.Instance);
    }

    [Fact]
    public async Task ReportIsMarkedAsModelledAndNeedsNoKey()
    {
        var report = await Service(StubHttp.Json(_ => Body())).GetReportAsync(Id);

        Assert.Equal(StationStatus.Ok, report!.Status);
        Assert.True(report.Modelled);
        Assert.Equal(90, report.HistoryMaxGapMinutes);
        Assert.Equal("Open-Meteo", report.Source);
    }

    [Fact]
    public async Task CurrentReadingConvertsUnitsAndRates()
    {
        var reading = (await Service(StubHttp.Json(_ => Body())).GetReportAsync(Id))!.Current!;

        Assert.Equal("Topsham, ME", reading.StationName);
        Assert.Equal(39.9, reading.TemperatureF);
        Assert.Equal(33.5, reading.FeelsLikeF);
        Assert.Equal(29.53, reading.PressureInHg!.Value, 2);
        Assert.Equal(0.04, reading.RainRateInPerHour!.Value, 5);
        Assert.Null(reading.IndoorTemperatureF);
        Assert.Null(reading.WeeklyRainIn);
    }

    [Fact]
    public async Task DailyTotalsCoverLocalMidnightToNowOnly()
    {
        var reading = (await Service(StubHttp.Json(_ => Body())).GetReportAsync(Id))!.Current!;

        Assert.Equal(0.3, reading.DailyRainIn!.Value, 5);
        Assert.Equal(14, reading.MaxDailyGustMph);
    }

    [Fact]
    public async Task HistoryIsTheLast24HoursWithoutFutureHours()
    {
        var history = (await Service(StubHttp.Json(_ => Body())).GetReportAsync(Id))!.History;

        Assert.Equal(
            [Unix(29, 17), Unix(30, 3), Unix(30, 5), Unix(30, 16)],
            history.Select(point => point.Time.ToUnixTimeSeconds()));
    }

    [Fact]
    public void OnlyFiveDigitZipsAreOpenMeteoIds()
    {
        var http = StubHttp.Json(_ => Body());
        var options = TestData.Options();
        var directory = new StationDirectory([], http, options, TestData.Locations(http, options), TimeProvider.System);

        Assert.IsType<OpenMeteoStation>(directory.Find(Id));
        Assert.Null(directory.Find("om-1234"));
        Assert.Null(directory.Find("om-123456"));
    }

    [Fact]
    public async Task BadResponseIsAnErrorNotACrash()
    {
        var report = await Service(StubHttp.Json(_ => "{}")).GetReportAsync(Id);

        Assert.Equal(StationStatus.Error, report!.Status);
        Assert.Null(report.Current);
    }
}
