using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class NewsCenterMaineParserTests
{
    private static string Row(string date, int high, string low = "38") => $"""
        <div class="weather-10-day__row">
            <div class="weather-10-day__row-container weather-10-day__row-container_interactive_false">
                <div class="weather-10-day__item weather-10-day__datetime">
                    <span class="weather-10-day__day weather-10-day__day_visible_true">Fri</span>
                    <span class="weather-10-day__date weather-10-day__date_visible_true">{date}</span>
                </div>
                <div class="weather-10-day__temperature">
                    <div class="weather-10-day__temperature-high">{high}</div>
                    <div class="weather-10-day__temperature-low">{low}</div>
                </div>
            </div>
        </div>
        """;

    private static string Page(string portlandRows, string bangorRows) => $"""
        <html><body>
        <h3 class="grid__module-heading">Portland 10-Day
        </h3>
        <div data-module="weather-10-day" class="weather-10-day">{portlandRows}</div>
        <h3 class="grid__module-heading">Bangor 10-Day
        </h3>
        <div data-module="weather-10-day" class="weather-10-day">{bangorRows}</div>
        </body></html>
        """;

    [Fact]
    public void ReadsEachSectionSeparately()
    {
        var html = Page(Row("Oct 30", 66) + Row("Oct 31", 52, "41"), Row("Oct 30", 64, "33"));
        var today = new DateOnly(2026, 10, 30);

        // Each row's low is the night after its high, so it lands on the next date.
        Assert.Equal(
            [
                new DailyTemperatures(new DateOnly(2026, 10, 30), 66, null),
                new DailyTemperatures(new DateOnly(2026, 10, 31), 52, 38),
                new DailyTemperatures(new DateOnly(2026, 11, 1), null, 41),
            ],
            NewsCenterMaineParser.Parse(html, "Portland 10-Day", today));
        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 30), 64, null), new DailyTemperatures(new DateOnly(2026, 10, 31), null, 33)],
            NewsCenterMaineParser.Parse(html, "Bangor 10-Day", today));
    }

    [Fact]
    public void KeepsARowWithOnlyOneTemperature()
    {
        var html = Page(Row("Oct 30", 66, "--"), Row("Oct 30", 64));

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 10, 30), 66, null)],
            NewsCenterMaineParser.Parse(html, "Portland 10-Day", new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void MovesTheLastNightsLowIntoTheNextYear()
    {
        var html = Page(Row("Dec 31", 30, "18"), Row("Dec 31", 25));

        Assert.Equal(
            [new DailyTemperatures(new DateOnly(2026, 12, 31), 30, null), new DailyTemperatures(new DateOnly(2027, 1, 1), null, 18)],
            NewsCenterMaineParser.Parse(html, "Portland 10-Day", new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void RollsDatesIntoTheNextYear()
    {
        var html = Page(Row("Dec 31", 30) + Row("Jan 01", 28), Row("Dec 31", 25));

        var highs = NewsCenterMaineParser.Parse(html, "Portland 10-Day", new DateOnly(2026, 12, 31));

        Assert.Equal(new DateOnly(2026, 12, 31), highs[0].Date);
        Assert.Equal(new DateOnly(2027, 1, 1), highs[1].Date);
    }

    [Fact]
    public void ThrowsWhenThePageNoLongerMatches()
    {
        Assert.Throws<FormatException>(() =>
            NewsCenterMaineParser.Parse("<html></html>", "Portland 10-Day", new DateOnly(2026, 10, 30)));
    }
}
