using System.Globalization;
using HtmlAgilityPack;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public static class NewsCenterMaineParser
{
    private const string RowClass = "weather-10-day__row";
    private const string DateClass = "weather-10-day__date";
    private const string HighClass = "weather-10-day__temperature-high";

    public static IReadOnlyList<DailyHigh> Parse(string html, string section, DateOnly today)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var module = document.DocumentNode.SelectSingleNode(
            $"//h3[contains(normalize-space(.), '{section}')]/following::div[@data-module='weather-10-day'][1]")
            ?? throw new FormatException($"Section '{section}' was not found.");

        var results = new List<DailyHigh>();
        foreach (var row in module.SelectNodes($".//div[{HasClass(RowClass)}]") ?? Enumerable.Empty<HtmlNode>())
        {
            var date = row.SelectSingleNode($".//span[{HasClass(DateClass)}]")?.InnerText.Trim();
            var high = row.SelectSingleNode($".//div[{HasClass(HighClass)}]")?.InnerText.Trim();

            if (date is not null && int.TryParse(high, CultureInfo.InvariantCulture, out var highF))
            {
                results.Add(new DailyHigh(ToDate(date, today), highF));
            }
        }

        return results.Count > 0 ? results : throw new FormatException($"Section '{section}' had no forecast rows.");
    }

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
