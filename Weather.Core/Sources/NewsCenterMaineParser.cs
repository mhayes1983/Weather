using System.Globalization;
using HtmlAgilityPack;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public static class NewsCenterMaineParser
{
    private const string RowClass = "weather-10-day__row";
    private const string DateClass = "weather-10-day__date";
    private const string HighClass = "weather-10-day__temperature-high";
    private const string LowClass = "weather-10-day__temperature-low";

    public static IReadOnlyList<DailyTemperatures> Parse(string html, string section, DateOnly today)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var module = document.DocumentNode.SelectSingleNode(
            $"//h3[contains(normalize-space(.), '{section}')]/following::div[@data-module='weather-10-day'][1]")
            ?? throw new FormatException($"Section '{section}' was not found.");

        var highs = new Dictionary<DateOnly, int>();
        var lows = new Dictionary<DateOnly, int>();
        foreach (var row in module.SelectNodes($".//div[{HasClass(RowClass)}]") ?? Enumerable.Empty<HtmlNode>())
        {
            var text = row.SelectSingleNode($".//span[{HasClass(DateClass)}]")?.InnerText.Trim();
            if (text is null)
            {
                continue;
            }

            // A row's low is the night after its high, so it goes on the next morning, matching the
            // other sources' calendar-day lows.
            var date = ToDate(text, today);
            if (ReadTemperature(row, HighClass) is int high)
            {
                highs.TryAdd(date, high);
            }

            if (ReadTemperature(row, LowClass) is int low)
            {
                lows.TryAdd(date.AddDays(1), low);
            }
        }

        var results = highs.Keys.Union(lows.Keys)
            .Order()
            .Select(date => new DailyTemperatures(
                date,
                highs.TryGetValue(date, out var high) ? high : null,
                lows.TryGetValue(date, out var low) ? low : null))
            .ToList();

        return results.Count > 0 ? results : throw new FormatException($"Section '{section}' had no forecast rows.");
    }

    private static int? ReadTemperature(HtmlNode row, string className) =>
        int.TryParse(row.SelectSingleNode($".//div[{HasClass(className)}]")?.InnerText.Trim(), CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string HasClass(string name) =>
        $"contains(concat(' ', normalize-space(@class), ' '), ' {name} ')";

    // The page shows dates such as "Jan 02" with no year.
    private static DateOnly ToDate(string text, DateOnly today)
    {
        var parsed = DateTime.ParseExact(text, "MMM d", CultureInfo.InvariantCulture);
        var date = new DateOnly(today.Year, parsed.Month, parsed.Day);
        return date.DayNumber < today.DayNumber - 30 ? date.AddYears(1) : date;
    }
}
