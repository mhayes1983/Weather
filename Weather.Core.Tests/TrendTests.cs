using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class TrendTests
{
    private static readonly DateTimeOffset Now = TestData.Now;

    private static StationHistoryPoint Point(double minutesAgo, double? temperatureF = null, double? pressureInHg = null) =>
        new(Now.AddMinutes(-minutesAgo), temperatureF, null, null, pressureInHg, null);

    private static StationReading Reading(double? temperatureF = null, double? pressureInHg = null) =>
        new() { ObservedAt = Now, TemperatureF = temperatureF, PressureInHg = pressureInHg };

    [Fact]
    public void HighAndLowComeWithTheirTimes()
    {
        var trends = TrendCalculator.From(Reading(55), [Point(600, 41.5), Point(300, 62.1), Point(5, 56)])!;

        Assert.Equal(62.1, trends.HighF);
        Assert.Equal(Now.AddMinutes(-300), trends.HighAt);
        Assert.Equal(41.5, trends.LowF);
        Assert.Equal(Now.AddMinutes(-600), trends.LowAt);
    }

    [Fact]
    public void LatestReadingCanBeTheHigh()
    {
        var trends = TrendCalculator.From(Reading(70), [Point(60, 65)])!;

        Assert.Equal(70, trends.HighF);
        Assert.Equal(Now, trends.HighAt);
    }

    [Fact]
    public void PointsAfterTheReadingAreIgnored()
    {
        var trends = TrendCalculator.From(Reading(50), [Point(-60, 90, 31)])!;

        Assert.Equal(50, trends.HighF);
        Assert.Null(trends.PressureTrend);
    }

    [Theory]
    [InlineData(30.08, 30.10, PressureTrend.Rising)]
    [InlineData(30.09, 30.10, PressureTrend.Steady)]
    [InlineData(30.10, 30.09, PressureTrend.Steady)]
    [InlineData(30.10, 30.08, PressureTrend.Falling)]
    [InlineData(30.00, 30.06, PressureTrend.RisingRapidly)]
    [InlineData(30.00, 29.94, PressureTrend.FallingRapidly)]
    public void PressureTrendIsTheChangeOverThreeHours(double earlier, double latest, PressureTrend expected)
    {
        var trends = TrendCalculator.From(Reading(pressureInHg: latest), [Point(180, pressureInHg: earlier), Point(60, pressureInHg: 28)])!;

        Assert.Equal(expected, trends.PressureTrend);
        Assert.Equal(Math.Round(latest - earlier, 2), trends.PressureChangeInHg);
    }

    [Fact]
    public void PressureUsesTheNearestPointToThreeHoursAgo()
    {
        var trends = TrendCalculator.From(
            Reading(pressureInHg: 30.10),
            [Point(205, pressureInHg: 29), Point(170, pressureInHg: 30.05), Point(155, pressureInHg: 31)])!;

        Assert.Equal(0.05, trends.PressureChangeInHg);
    }

    [Fact]
    public void NoTrendWithoutAPointNearThreeHoursAgo()
    {
        var trends = TrendCalculator.From(Reading(50, 30.10), [Point(100, pressureInHg: 30), Point(240, pressureInHg: 30)])!;

        Assert.Null(trends.PressureChangeInHg);
        Assert.Null(trends.PressureTrend);
    }

    [Fact]
    public void HistoryPressureStandsInWhenTheReadingHasNone()
    {
        var trends = TrendCalculator.From(Reading(50), [Point(190, pressureInHg: 30), Point(10, pressureInHg: 29.9)])!;

        Assert.Equal(-0.1, trends.PressureChangeInHg);
        Assert.Equal(PressureTrend.FallingRapidly, trends.PressureTrend);
    }

    [Fact]
    public void NothingToReportIsNull()
    {
        Assert.Null(TrendCalculator.From(Reading(), [Point(10)]));
    }
}
