using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>Totals a source's snow and liquid over whole local dates, and finds its strongest gusts and deepest snow there.</summary>
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

    /// <summary>Gusts from this speed count as windy for the timing bars.</summary>
    public const double WindyMph = 30;

    /// <summary>Below this much liquid a snow-to-liquid ratio means little.</summary>
    public const double RatioMinimumLiquidInches = 0.05;

    /// <summary>
    /// Snow with liquid well beyond what it accounts for: under 5 inches of snow for each inch of water,
    /// where 10:1 is typical and 5:1 is already very wet, so some of the liquid is rain or sleet.
    /// </summary>
    public const double MixedRatio = 5;
    public const double MixedMinimumLiquidInches = 0.1;

    /// <summary>
    /// The strongest gust in any period touching the window, and that period cut at the window's edges. Null when
    /// the source has none there. Partial when its gusts start after the window does or end before it.
    /// </summary>
    public static (double Mph, DateTimeOffset PeakStart, DateTimeOffset PeakEnd, bool Partial)? MaxGust(
        IReadOnlyList<GustPeriod> gusts, DateTimeOffset start, DateTimeOffset end)
    {
        var inside = gusts.Where(gust => gust.End > start && gust.Start < end && gust.End > gust.Start).ToList();
        if (inside.Count == 0)
        {
            return null;
        }

        // The earliest of equal gusts, so the time doesn't jump between refreshes.
        var peak = inside.OrderByDescending(gust => gust.Mph).ThenBy(gust => gust.Start).First();
        var partial = gusts.Min(gust => gust.Start) > start || gusts.Max(gust => gust.End) < end;
        return (Math.Round(peak.Mph), peak.Start > start ? peak.Start : start, peak.End < end ? peak.End : end, partial);
    }

    /// <summary>When gusts first and last reach <see cref="WindyMph"/> inside the window, or null when they never do.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End)? Windy(
        IReadOnlyList<GustPeriod> gusts, DateTimeOffset start, DateTimeOffset end)
    {
        var windy = gusts.Where(gust => gust.End > start && gust.Start < end && gust.Mph >= WindyMph).ToList();
        if (windy.Count == 0)
        {
            return null;
        }

        var first = windy.Min(gust => gust.Start);
        var last = windy.Max(gust => gust.End);
        return (first > start ? first : start, last < end ? last : end);
    }

    /// <summary>The gusts inside the window, oldest first, cut at its edges. A gust isn't shared out like an amount.</summary>
    public static IReadOnlyList<GustPeriod> Clip(IReadOnlyList<GustPeriod> gusts, DateTimeOffset start, DateTimeOffset end) =>
        gusts
            .Where(gust => gust.End > start && gust.Start < end && gust.End > gust.Start)
            .OrderBy(gust => gust.Start)
            .Select(gust => gust with { Start = gust.Start > start ? gust.Start : start, End = gust.End < end ? gust.End : end })
            .ToList();

    /// <summary>The depth points from the window's start to its end, oldest first.</summary>
    public static IReadOnlyList<DepthPoint> Clip(IReadOnlyList<DepthPoint> depth, DateTimeOffset start, DateTimeOffset end) =>
        depth.Where(point => point.Time >= start && point.Time <= end).OrderBy(point => point.Time).ToList();

    /// <summary>
    /// Snow on the ground: at the point nearest <paramref name="now"/> (within 90 minutes), the most during the
    /// window and when, and at its end. Null when the model has no depth in the window. Partial when it ends first.
    /// </summary>
    public static (double? Now, double Max, DateTimeOffset MaxAt, double End, bool Partial)? Depth(
        IReadOnlyList<DepthPoint> depth, DateTimeOffset now, DateTimeOffset start, DateTimeOffset end)
    {
        var inside = Clip(depth, start, end);
        if (inside.Count == 0)
        {
            return null;
        }

        var nearest = depth.MinBy(point => Math.Abs((point.Time - now).TotalMinutes));
        double? current = nearest is not null && Math.Abs((nearest.Time - now).TotalMinutes) <= 90 ? nearest.Inches : null;
        var max = inside.OrderByDescending(point => point.Inches).ThenBy(point => point.Time).First();
        var last = inside[^1];
        return (current, max.Inches, max.Time, last.Inches, last.Time < end.AddHours(-1));
    }

    /// <summary>
    /// Snow over liquid for the same source and window, such as 10 for 10:1, and whether it looks mixed.
    /// No ratio when either total is partial or there's too little liquid to say.
    /// </summary>
    public static (double? Ratio, bool Mixed) Ratio(
        (double Low, double High, bool Partial)? snow, (double Low, double High, bool Partial)? liquid)
    {
        if (snow is not { Partial: false } snowTotal || liquid is not { Partial: false } liquidTotal
            || liquidTotal.High < RatioMinimumLiquidInches || snowTotal.High <= 0)
        {
            // No snow at all is plain rain, which the Rain view already shows.
            return (null, false);
        }

        var ratio = snowTotal.High / liquidTotal.High;
        var mixed = liquidTotal.High >= MixedMinimumLiquidInches && ratio < MixedRatio;
        return (Math.Round(ratio), mixed);
    }

    private static double Round(double inches, int digits) => Math.Round(inches, digits, MidpointRounding.AwayFromZero);

    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
