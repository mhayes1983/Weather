namespace Weather.Core.Models;

/// <summary>
/// One line of an almanac day. Temperatures are °F and amounts are inches.
/// A trace of rain or snow is <see cref="TraceInches"/>, so it isn't confused with none.
/// </summary>
public sealed record AlmanacRow(double? Value, double? Average, double? Record, int? RecordYear)
{
    public const double TraceInches = 0.001;
}

/// <summary>Each row's value is the forecast for today and later, and what was measured for an earlier day.</summary>
public sealed record AlmanacDay(DateOnly Date, AlmanacRow High, AlmanacRow Low, AlmanacRow Precip, AlmanacRow Snow);

/// <summary>
/// <see cref="Days"/> is yesterday, today and tomorrow, in date order. Averages and records come
/// from <see cref="ClimateStation"/>, a public climate station near the weather station.
/// </summary>
public sealed record AlmanacReport(
    string StationName,
    SourceStatus Status,
    string? Message,
    string? ClimateStation,
    int? RecordsSince,
    DateOnly? Today,
    IReadOnlyList<AlmanacDay> Days);
