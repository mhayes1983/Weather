using Weather.Core.Models;

namespace Weather.Core.Sources;

/// <summary>
/// Joins forecasts given as day and night parts into one entry per date: the day's high, and the low
/// of the night that ends that morning.
/// </summary>
internal static class DayParts
{
    public static List<DailyTemperatures> Merge(IReadOnlyDictionary<DateOnly, int> highs, IReadOnlyDictionary<DateOnly, int> lows) =>
        highs.Keys.Union(lows.Keys)
            .Order()
            .Select(date => new DailyTemperatures(
                date,
                highs.TryGetValue(date, out var high) ? high : null,
                lows.TryGetValue(date, out var low) ? low : null))
            .ToList();
}
