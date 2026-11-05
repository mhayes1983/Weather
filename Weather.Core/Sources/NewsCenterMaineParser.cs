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

        var results = new List<DailyTemperatures>();
        foreach (var row in module.SelectNodes($".//div[{HasClass(RowClass)}]") ?? Enumerable.Empty<HtmlNode>())
        {
            var date = row.SelectSingleNode($".//span[{HasClass(DateClass)}]")?.InnerText.Trim();
            var high = ReadTemperature(row, HighClass);
            var low = ReadTemperature(row, LowClass);

            if (date is not null && (high is not null || low is not null))
            {
                results.Add(new DailyTemperatures(ToDate(date, today), high, low));
            }
        }

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
