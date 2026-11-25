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
/// <summary><see cref="Tides"/> is null when there's no tide station nearby or its predictions couldn't be loaded.</summary>
public sealed record AlmanacDay(
    DateOnly Date,
    AlmanacRow High,
    AlmanacRow Low,
    AlmanacRow Precip,
    AlmanacRow Snow,
    AlmanacSky? Sky = null,
    IReadOnlyList<AlmanacTide>? Tides = null);

/// <summary>
/// Sun and moon times for an almanac day, as local "HH:mm". The moon doesn't rise or set every day.
/// <see cref="Moon"/> is null when USNO couldn't be reached, which leaves the forecast's sun times.
/// Daylight is sunset minus sunrise as shown, and its change is from the day before, so both agree with the times.
/// <see cref="DaylightSinceSolsticeMinutes"/> is the change since the last solstice, negative after the summer one.
/// </summary>
public sealed record AlmanacSky(
    string? Sunrise,
    string? Sunset,
    MoonInfo? Moon,
    int? DaylightMinutes = null,
    int? DaylightChangeMinutes = null,
    int? DaylightSinceSolsticeMinutes = null);

/// <summary>A predicted high or low tide at local "HH:mm", in feet above mean lower low water.</summary>
public sealed record AlmanacTide(string Time, bool High, double HeightFt);

/// <summary>
/// <see cref="Days"/> is yesterday, today and tomorrow, in date order. Averages and records come
/// from <see cref="ClimateStation"/>, a public climate station near the weather station. Tides are
/// predictions for <see cref="TideStation"/>, a public NOAA tide station, named so the page can credit it.
/// </summary>
public sealed record AlmanacReport(
    string StationName,
    SourceStatus Status,
    string? Message,
    string? ClimateStation,
    int? RecordsSince,
    DateOnly? Today,
    IReadOnlyList<AlmanacDay> Days,
    string? TideStation = null);
