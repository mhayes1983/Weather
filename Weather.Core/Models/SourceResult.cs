namespace Weather.Core.Models;

public enum SourceStatus
{
    Ok,
    NeedsKey,
    Error,
}

public sealed record SourceResult(
    SourceStatus Status,
    IReadOnlyList<DailyHigh> Highs,
    DateTimeOffset? FetchedAt,
    string? Message)
{
    public static SourceResult Ok(IReadOnlyList<DailyHigh> highs, DateTimeOffset fetchedAt) =>
        new(SourceStatus.Ok, highs, fetchedAt, null);

    public static SourceResult NeedsKey() =>
        new(SourceStatus.NeedsKey, [], null, "Needs key");

    public static SourceResult Error(string message) =>
        new(SourceStatus.Error, [], null, message);
}
