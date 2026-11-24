namespace Weather.Core.Models;

/// <summary>Snow, or liquid precipitation, expected from <see cref="Start"/> to <see cref="End"/>, as a range in inches.</summary>
public sealed record SnowPeriod(DateTimeOffset Start, DateTimeOffset End, double LowInches, double HighInches);

/// <summary>The strongest gust expected from <see cref="Start"/> to <see cref="End"/>, in mph.</summary>
public sealed record GustPeriod(DateTimeOffset Start, DateTimeOffset End, double Mph);

/// <summary>Modelled snow on the ground at <see cref="Time"/>, in inches.</summary>
public sealed record DepthPoint(DateTimeOffset Time, double Inches);

/// <summary>
/// One source's forecast for the Storm Forecast page. Each series is null when the source doesn't forecast it.
/// <see cref="Ice"/> is ice build-up in the same form as the snow. <see cref="Liquid"/> is all precipitation
/// as water, rain plus melted snow, because only Open-Meteo separates rain.
/// </summary>
public sealed record SnowResult(
    SourceStatus Status,
    IReadOnlyList<SnowPeriod> Periods,
    DateTimeOffset? FetchedAt,
    string? Message,
    IReadOnlyList<SnowPeriod>? Ice = null,
    IReadOnlyList<SnowPeriod>? Liquid = null,
    IReadOnlyList<GustPeriod>? Gusts = null,
    IReadOnlyList<DepthPoint>? Depth = null)
{
    public static SnowResult Ok(
        IReadOnlyList<SnowPeriod> periods,
        DateTimeOffset fetchedAt,
        IReadOnlyList<SnowPeriod>? ice = null,
        IReadOnlyList<SnowPeriod>? liquid = null,
        IReadOnlyList<GustPeriod>? gusts = null,
        IReadOnlyList<DepthPoint>? depth = null) =>
        new(SourceStatus.Ok, periods, fetchedAt, null, ice, liquid, gusts, depth);

    public static SnowResult NeedsKey() =>
        new(SourceStatus.NeedsKey, [], null, "Needs key");

    public static SnowResult Error(string message) =>
        new(SourceStatus.Error, [], null, message);
}

/// <summary>A town on the Storm Forecast page. <see cref="Message"/> says why it has no values.</summary>
public sealed record SnowTown(string Zip, string Name, string? Message);

/// <summary>A configured town offered before anything loads.</summary>
public sealed record SnowTownSummary(string Zip, string Name);

/// <summary>The Storm Forecast page's views. Every source has snow and rain; only some have wind or depth.</summary>
public static class StormViews
{
    public const string Snow = "snow";
    public const string Rain = "rain";
    public const string Wind = "wind";
    public const string Depth = "depth";
}

/// <summary>
/// One source's row. The status is NeedsKey or Error only when every town has it. <see cref="Views"/> lists the
/// views the source has data for, so a model without gusts isn't shown as a blank wind row.
/// </summary>
public sealed record SnowSourceRow(
    string Id,
    string Label,
    SourceStatus Status,
    string? Message,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<string>? Views = null);

/// <summary>
/// A source's total for one town over the whole range, of snow or of liquid. <see cref="Partial"/> means the
/// source's forecast doesn't cover the whole range, so the total is a minimum.
/// <see cref="SnowStart"/> and <see cref="SnowEnd"/> are when it first and last has snow (or rain) falling inside
/// the range, in the town's local time, and are null when it has none. <see cref="IceInches"/> is null
/// for a source that doesn't forecast ice, and always for liquid. <see cref="TimingLowInches"/> and
/// <see cref="TimingHighInches"/> are the amount from <see cref="SnowStart"/> to <see cref="SnowEnd"/>.
/// <see cref="PeakStart"/> to <see cref="PeakEnd"/> is the source's heaviest period, which is an hour for hourly
/// sources and a 6 to 12-hour block for NWS and Weather Underground. <see cref="Ratio"/> is the source's snow over
/// its liquid (10 means 10:1), null when there's too little liquid to say or either total is partial.
/// <see cref="Mixed"/> means it has snow but far more liquid than the snow accounts for, so rain or a mix is likely.
/// </summary>
public sealed record SnowCell(
    string SourceId,
    string Zip,
    double? LowInches,
    double? HighInches,
    bool Partial,
    SourceStatus Status,
    string? Message,
    DateTimeOffset? SnowStart = null,
    DateTimeOffset? SnowEnd = null,
    double? IceInches = null,
    double? TimingLowInches = null,
    double? TimingHighInches = null,
    DateTimeOffset? PeakStart = null,
    DateTimeOffset? PeakEnd = null,
    double? PeakInchesPerHour = null,
    double? Ratio = null,
    bool Mixed = false);

/// <summary>
/// A source's strongest gust for one town over the range, in mph. <see cref="WindyStart"/> and
/// <see cref="WindyEnd"/> are when gusts first and last reach 30 mph, in the town's local time.
/// <see cref="PeakStart"/> to <see cref="PeakEnd"/> is the period with the strongest gust.
/// </summary>
public sealed record WindCell(
    string SourceId,
    string Zip,
    double? MaxMph,
    bool Partial,
    SourceStatus Status,
    string? Message,
    DateTimeOffset? WindyStart = null,
    DateTimeOffset? WindyEnd = null,
    DateTimeOffset? PeakStart = null,
    DateTimeOffset? PeakEnd = null);

/// <summary>
/// A model's snow on the ground for one town: <see cref="NowInches"/> at the current hour, the most during the
/// range and when, and at its end. Modelled, never measured. <see cref="Partial"/> means the model ends before the range does.
/// </summary>
public sealed record DepthCell(
    string SourceId,
    string Zip,
    double? NowInches,
    double? MaxInches,
    DateTimeOffset? MaxAt,
    double? EndInches,
    bool Partial,
    SourceStatus Status,
    string? Message);

/// <summary>
/// One source's periods for one town, trimmed to the range and in the town's local time.
/// Hourly sources give hours; NWS and Weather Underground give longer blocks. Each series is null for a
/// source that doesn't forecast it. The partial flags mean that series doesn't cover the whole range,
/// as in <see cref="SnowCell.Partial"/>, so its total is a minimum.
/// </summary>
public sealed record SnowHourlySource(
    string Id,
    string Label,
    SourceStatus Status,
    string? Message,
    IReadOnlyList<SnowPeriod> Periods,
    IReadOnlyList<SnowPeriod>? Ice,
    bool Partial = false,
    IReadOnlyList<SnowPeriod>? Liquid = null,
    bool LiquidPartial = false,
    IReadOnlyList<GustPeriod>? Gusts = null,
    bool GustsPartial = false,
    IReadOnlyList<DepthPoint>? Depth = null);

/// <summary>
/// Every source's periods for one town. <see cref="Now"/> is the town's local time.
/// <see cref="Message"/> says why the town has no sources, such as an unknown ZIP.
/// </summary>
public sealed record SnowHourly(
    string Zip,
    string Name,
    string? Message,
    DateOnly Start,
    DateOnly End,
    DateTimeOffset Now,
    IReadOnlyList<SnowHourlySource> Sources);

/// <summary>Every view's cells at once, so switching views on the page needs no more calls.</summary>
public sealed record SnowTable(
    DateOnly Start,
    DateOnly End,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SnowTown> Towns,
    IReadOnlyList<SnowSourceRow> Sources,
    IReadOnlyList<SnowCell> Cells,
    IReadOnlyList<SnowCell>? RainCells = null,
    IReadOnlyList<WindCell>? WindCells = null,
    IReadOnlyList<DepthCell>? DepthCells = null);

/// <summary>
/// A measured or estimated amount someone reported to the NWS: inches of snow or rain, or a gust in mph.
/// <see cref="ObservedAt"/> is in the town's local time. <see cref="Place"/> is the NWS's own wording, such as "2 NNE Brunswick".
/// </summary>
public sealed record SnowReport(
    string Place,
    double Amount,
    DateTimeOffset ObservedAt,
    double DistanceMiles,
    string? Source,
    bool Estimated);

/// <summary>The reports near one town, nearest first, with the lowest and highest of them.</summary>
public sealed record SnowTownReports(
    string Zip,
    double? Low,
    double? High,
    IReadOnlyList<SnowReport> Reports);

/// <summary>
/// What was reported near each town: snow in inches and gusts in mph for the range so far, and rain in inches
/// for the past week from <see cref="RainSince"/> through today, whatever the range, because a forecast's dates
/// are mostly still to come. An Error status means some towns couldn't be checked, and the lists hold only the ones that could.
/// </summary>
public sealed record SnowReportTable(
    SourceStatus Status,
    string? Message,
    double RadiusMiles,
    IReadOnlyList<SnowTownReports> Towns,
    IReadOnlyList<SnowTownReports>? RainTowns = null,
    IReadOnlyList<SnowTownReports>? WindTowns = null,
    DateOnly? RainSince = null);

/// <summary>Which view an alert belongs to.</summary>
public enum StormAlertKind
{
    Winter,
    Flood,
    Wind,
}

public sealed record SnowAlert(WeatherAlert Alert, IReadOnlyList<string> Zips, StormAlertKind Kind = StormAlertKind.Winter);

/// <summary>
/// Winter, flood and wind alerts for the towns. An Error status names the towns that couldn't be checked,
/// and the alerts list holds only the towns that could.
/// </summary>
public sealed record SnowAlertReport(SourceStatus Status, string? Message, IReadOnlyList<SnowAlert> Alerts);
