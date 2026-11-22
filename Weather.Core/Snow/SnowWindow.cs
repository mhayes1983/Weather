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
        IReadOnlyList<SnowPeriod> periods, DateTimeOffset start, DateTimeOffset end, int digits = 1)
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
        return (Round(low, digits), Round(high, digits), coversFrom > start || coversUntil < end);
    }

    /// <summary>
    /// When snow first and last falls inside the window, or null when none does. A period counts when its
    /// high amount is at least <see cref="FallingInchesPerHour"/> for each hour it lasts, which leaves out
    /// flurries. Sources with 12-hour periods can only place the snow to the nearest 12 hours.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End)? Timing(
        IReadOnlyList<SnowPeriod> periods, DateTimeOffset start, DateTimeOffset end)
    {
        var falling = periods
            .Where(period => period.End > start && period.Start < end)
            .Where(period => period.End > period.Start
                && period.HighInches / (period.End - period.Start).TotalHours >= FallingInchesPerHour)
            .ToList();
        if (falling.Count == 0)
        {
            return null;
        }

        var first = falling.Min(period => period.Start);
        var last = falling.Max(period => period.End);
        return (first > start ? first : start, last < end ? last : end);
    }

    /// <summary>
    /// The periods inside the window, oldest first, with any period that crosses an edge cut at it and
    /// its amounts cut by the same share <see cref="Total"/> takes.
    /// </summary>
    public static IReadOnlyList<SnowPeriod> Clip(IReadOnlyList<SnowPeriod> periods, DateTimeOffset start, DateTimeOffset end)
    {
        var clipped = new List<SnowPeriod>();
        foreach (var period in periods.OrderBy(period => period.Start))
        {
            var overlapStart = period.Start > start ? period.Start : start;
            var overlapEnd = period.End < end ? period.End : end;
            var length = (period.End - period.Start).TotalSeconds;
            if (overlapEnd <= overlapStart || length <= 0)
            {
                continue;
            }

            var share = (overlapEnd - overlapStart).TotalSeconds / length;
            clipped.Add(new SnowPeriod(
                overlapStart, overlapEnd, Round(period.LowInches * share, 2), Round(period.HighInches * share, 2)));
        }

        return clipped;
    }

    /// <summary>
    /// The falling period inside the window with the highest rate by its high amount, or null when none
    /// is falling. A period crossing an edge keeps its rate but its times are cut at the edge.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End, double InchesPerHour)? Peak(
        IReadOnlyList<SnowPeriod> periods, DateTimeOffset start, DateTimeOffset end)
    {
        (DateTimeOffset Start, DateTimeOffset End, double InchesPerHour)? peak = null;
        foreach (var period in periods.Where(period => period.End > start && period.Start < end && period.End > period.Start))
        {
            var rate = period.HighInches / (period.End - period.Start).TotalHours;
            if (rate >= FallingInchesPerHour && (peak is null || rate > peak.Value.InchesPerHour))
            {
                peak = (period.Start, period.End, rate);
            }
        }

        if (peak is not { } found)
        {
            return null;
        }

        return (found.Start > start ? found.Start : start, found.End < end ? found.End : end, Round(found.InchesPerHour, 2));
    }

    public const double FallingInchesPerHour = 0.01;

    private static double Round(double inches, int digits) => Math.Round(inches, digits, MidpointRounding.AwayFromZero);

    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
