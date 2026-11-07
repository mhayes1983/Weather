namespace Weather.Core.Models;

/// <summary>Snow expected from <see cref="Start"/> to <see cref="End"/>, as a range in inches.</summary>
public sealed record SnowPeriod(DateTimeOffset Start, DateTimeOffset End, double LowInches, double HighInches);

public sealed record SnowResult(
    SourceStatus Status,
    IReadOnlyList<SnowPeriod> Periods,
    DateTimeOffset? FetchedAt,
    string? Message)
{
    public static SnowResult Ok(IReadOnlyList<SnowPeriod> periods, DateTimeOffset fetchedAt) =>
        new(SourceStatus.Ok, periods, fetchedAt, null);

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
/// </summary>
public sealed record SnowCell(
    string SourceId,
    string Zip,
    double? LowInches,
    double? HighInches,
    bool Partial,
    SourceStatus Status,
    string? Message);

public sealed record SnowTable(
    DateOnly Start,
    DateOnly End,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SnowTown> Towns,
    IReadOnlyList<SnowSourceRow> Sources,
    IReadOnlyList<SnowCell> Cells);

public sealed record SnowAlert(WeatherAlert Alert, IReadOnlyList<string> Zips);

/// <summary>
/// Winter alerts for the towns. An Error status names the towns that couldn't be checked,
/// and the alerts list holds only the towns that could.
/// </summary>
public sealed record SnowAlertReport(SourceStatus Status, string? Message, IReadOnlyList<SnowAlert> Alerts);
