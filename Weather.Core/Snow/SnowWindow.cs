using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>Totals a source's snow periods over whole local dates.</summary>
public static class SnowWindow
{
    public static (DateTimeOffset Start, DateTimeOffset End) Bounds(DateOnly start, DateOnly end, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return (LocalMidnight(start, zone), LocalMidnight(end.AddDays(1), zone));
    }

    /// <summary>
    /// Adds up the periods inside the window, taking a share of any period that crosses an edge.
    /// Null when the source has nothing for the window. Partial when it starts after the window does or ends before it.
    /// </summary>
    public static (double Low, double High, bool Partial)? Total(
        IReadOnlyList<SnowPeriod> periods, DateTimeOffset start, DateTimeOffset end)
    {
        double low = 0, high = 0;
        var any = false;
        foreach (var period in periods)
        {
            var overlapStart = period.Start > start ? period.Start : start;
            var overlapEnd = period.End < end ? period.End : end;
            var length = (period.End - period.Start).TotalSeconds;
            if (overlapEnd <= overlapStart || length <= 0)
            {
                continue;
            }

            var share = (overlapEnd - overlapStart).TotalSeconds / length;
            low += period.LowInches * share;
            high += period.HighInches * share;
            any = true;
        }

        if (!any)
        {
            return null;
        }

        // NWS periods start at the current one and WU drops today's day part once it's over, so a
        // source can miss the start of the window as well as its end.
        var coversFrom = periods.Min(period => period.Start);
        var coversUntil = periods.Max(period => period.End);
        return (Round(low), Round(high), coversFrom > start || coversUntil < end);
    }

    private static double Round(double inches) => Math.Round(inches, 1, MidpointRounding.AwayFromZero);

    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
