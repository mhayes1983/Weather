namespace Weather.Core.Models;

public sealed record WeatherAlert(
    string Id,
    string Event,
    string Severity,
    string? Headline,
    string? Description,
    string? Instruction,
    string? Starts,
    string? Ends);

public sealed record AlertReport(
    string StationName,
    SourceStatus Status,
    string? Message,
    IReadOnlyList<WeatherAlert> Alerts);
