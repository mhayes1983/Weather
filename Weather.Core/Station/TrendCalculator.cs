using Weather.Core.Models;

namespace Weather.Core.Station;

public static class TrendCalculator
{
    private static readonly TimeSpan PressurePeriod = TimeSpan.FromHours(3);

    // Hourly history can be half an hour either side of three hours ago.
    private static readonly TimeSpan PressureTolerance = TimeSpan.FromMinutes(30);

    // In inHg over three hours: 0.02 is about 0.7 hPa and 0.06 about 2 hPa.
    private const double SteadyBelowInHg = 0.02;
    private const double RapidFromInHg = 0.06;

    /// <summary>Returns null when there's nothing to report.</summary>
    public static StationTrends? From(StationReading current, IReadOnlyList<StationHistoryPoint> history)
    {
        // Some histories run ahead of the reading, and the reading can be newer than the last point.
        var past = history.Where(point => point.Time <= current.ObservedAt).ToList();

        var temperatures = past
            .Where(point => point.TemperatureF is not null)
            .Select(point => (point.Time, Value: point.TemperatureF!.Value))
            .ToList();
        if (current.TemperatureF is { } temperature)
        {
            temperatures.Add((current.ObservedAt, temperature));
        }

        var change = PressureChange(current, past);
        if (temperatures.Count == 0 && change is null)
        {
            return null;
        }

        (DateTimeOffset Time, double Value)? high = temperatures.Count == 0 ? null : temperatures.MaxBy(point => point.Value);
        (DateTimeOffset Time, double Value)? low = temperatures.Count == 0 ? null : temperatures.MinBy(point => point.Value);

        return new StationTrends(high?.Value, high?.Time, low?.Value, low?.Time, change, Classify(change));
    }

    private static double? PressureChange(StationReading current, List<StationHistoryPoint> past)
    {
        var latest = current.PressureInHg is { } pressure
            ? (Time: current.ObservedAt, Value: pressure)
            : past.Where(point => point.PressureInHg is not null)
                .Select(point => (point.Time, Value: point.PressureInHg!.Value))
                .LastOrDefault();
        if (latest == default)
        {
            return null;
        }

        var target = latest.Time - PressurePeriod;
        var earlier = past
            .Where(point => point.PressureInHg is not null && (point.Time - target).Duration() <= PressureTolerance)
            .MinBy(point => (point.Time - target).Duration());

        // Rounded so 30.10 - 30.08 is 0.02 and not a little under it.
        return earlier is null ? null : Math.Round(latest.Value - earlier.PressureInHg!.Value, 2);
    }

    private static PressureTrend? Classify(double? change) => change switch
    {
        null => null,
        >= RapidFromInHg => PressureTrend.RisingRapidly,
        >= SteadyBelowInHg => PressureTrend.Rising,
        <= -RapidFromInHg => PressureTrend.FallingRapidly,
        <= -SteadyBelowInHg => PressureTrend.Falling,
        _ => PressureTrend.Steady,
    };
}
