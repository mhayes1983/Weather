namespace Weather.Core.Models;

/// <summary>Snow expected from <see cref="Start"/> to <see cref="End"/>, as a range in inches.</summary>
public sealed record SnowPeriod(DateTimeOffset Start, DateTimeOffset End, double LowInches, double HighInches);

/// <summary><see cref="Ice"/> is ice build-up in the same form as the snow, or null when the source doesn't forecast it.</summary>
public sealed record SnowResult(
    SourceStatus Status,
    IReadOnlyList<SnowPeriod> Periods,
    DateTimeOffset? FetchedAt,
    string? Message,
    IReadOnlyList<SnowPeriod>? Ice = null)
{
    public static SnowResult Ok(IReadOnlyList<SnowPeriod> periods, DateTimeOffset fetchedAt, IReadOnlyList<SnowPeriod>? ice = null) =>
        new(SourceStatus.Ok, periods, fetchedAt, null, ice);

    public static SnowResult NeedsKey() =>
        new(SourceStatus.NeedsKey, [], null, "Needs key");

    public static SnowResult Error(string message) =>
        new(SourceStatus.Error, [], null, message);
}

/// <summary>A town on the Snow Forecast page. <see cref="Message"/> says why it has no values.</summary>
public sealed record SnowTown(string Zip, string Name, string? Message);

/// <summary>A configured town offered before anything loads.</summary>
public sealed record SnowTownSummary(string Zip, string Name);

/// <summary>One source's row. The status is NeedsKey or Error only when every town has it.</summary>
public sealed record SnowSourceRow(
    string Id,
    string Label,
    SourceStatus Status,
    string? Message,
    DateTimeOffset? FetchedAt);

/// <summary>
/// A source's total for one town over the whole range. <see cref="Partial"/> means the
/// source's forecast doesn't cover the whole range, so the total is a minimum.
/// <see cref="SnowStart"/> and <see cref="SnowEnd"/> are when it first and last has snow falling inside
/// the range, in the town's local time, and are null when it has none. <see cref="IceInches"/> is null
/// for a source that doesn't forecast ice.
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
    double? IceInches = null);

public sealed record SnowTable(
    DateOnly Start,
    DateOnly End,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SnowTown> Towns,
    IReadOnlyList<SnowSourceRow> Sources,
    IReadOnlyList<SnowCell> Cells);

/// <summary>
/// A measured or estimated snow total someone reported to the NWS. <see cref="ObservedAt"/> is in the
/// town's local time. <see cref="Place"/> is the NWS's own wording, such as "2 NNE Brunswick".
/// </summary>
public sealed record SnowReport(
    string Place,
    double Inches,
    DateTimeOffset ObservedAt,
    double DistanceMiles,
    string? Source,
    bool Estimated);

/// <summary>The reports near one town, nearest first, with the lowest and highest of them.</summary>
public sealed record SnowTownReports(
    string Zip,
    double? LowInches,
    double? HighInches,
    IReadOnlyList<SnowReport> Reports);

/// <summary>
/// What was reported near each town for the range so far. An Error status means some towns couldn't
/// be checked, and <see cref="Towns"/> holds only the ones that could.
/// </summary>
public sealed record SnowReportTable(
    SourceStatus Status,
    string? Message,
    double RadiusMiles,
    IReadOnlyList<SnowTownReports> Towns);

public sealed record SnowAlert(WeatherAlert Alert, IReadOnlyList<string> Zips);

/// <summary>
/// Winter alerts for the towns. An Error status names the towns that couldn't be checked,
/// and the alerts list holds only the towns that could.
/// </summary>
public sealed record SnowAlertReport(SourceStatus Status, string? Message, IReadOnlyList<SnowAlert> Alerts);
