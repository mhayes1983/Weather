namespace Weather.Core.Models;

public enum Anomaly
{
    None,
    Above,
    Below,
}

public sealed record ForecastCell(int? HighF, Anomaly HighAnomaly, int? LowF, Anomaly LowAnomaly);

public sealed record ForecastRow(
    DateOnly Date,
    int? AverageHighF,
    int? AverageLowF,
    IReadOnlyDictionary<string, ForecastCell> Cells);

public sealed record ForecastColumn(
    string Id,
    string Label,
    SourceStatus Status,
    string? Message,
    DateTimeOffset? FetchedAt);

public sealed record AverageRange(int StartF, int EndF, string Station);

public sealed record ForecastTable(
    ForecastLocation Location,
    DateTimeOffset GeneratedAt,
    AverageRange? AverageHigh,
    AverageRange? AverageLow,
    string? AverageMessage,
    int AboveAverageF,
    int BelowAverageF,
    IReadOnlyList<ForecastColumn> Columns,
    IReadOnlyList<ForecastRow> Rows);
