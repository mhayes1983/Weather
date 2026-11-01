namespace Weather.Core.Models;

public enum StationStatus
{
    Ok,
    Offline,
    NeedsKey,
    Error,
}

public enum StationHistoryRange
{
    Last24Hours,
    Today,
}

public sealed record StationSummary(string Id, string Name);

public sealed record StationMatch(string Id, string Name, double? DistanceMi);

public sealed record StationReading
{
    public string? StationName { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public double? TemperatureF { get; init; }
    public double? FeelsLikeF { get; init; }
    public double? DewPointF { get; init; }
    public double? HumidityPercent { get; init; }
    public double? IndoorTemperatureF { get; init; }
    public double? IndoorHumidityPercent { get; init; }
    public double? WindSpeedMph { get; init; }
    public double? WindGustMph { get; init; }
    public double? WindDirectionDegrees { get; init; }
    public double? MaxDailyGustMph { get; init; }
    public double? PressureInHg { get; init; }
    public double? RainRateInPerHour { get; init; }
    public double? EventRainIn { get; init; }
    public double? DailyRainIn { get; init; }
    public double? WeeklyRainIn { get; init; }
    public double? MonthlyRainIn { get; init; }
    public double? YearlyRainIn { get; init; }
    public DateTimeOffset? LastRain { get; init; }
    public double? UvIndex { get; init; }
    public double? SolarRadiationWm2 { get; init; }
    public bool? BatteryLow { get; init; }
}

public sealed record StationHistoryPoint(
    DateTimeOffset Time,
    double? TemperatureF,
    double? WindSpeedMph,
    double? WindGustMph,
    double? PressureInHg,
    double? RainRateInPerHour);

public sealed record StationReport(
    StationStatus Status,
    string? Message,
    DateTimeOffset GeneratedAt,
    string Source,
    StationReading? Current,
    IReadOnlyList<StationHistoryPoint> History,
    StationHistoryRange HistoryRange,
    string? HistoryMessage,
    bool Modelled = false,
    int HistoryMaxGapMinutes = StationReport.DefaultHistoryMaxGapMinutes,
    CurrentSky? Sky = null)
{
    public const int DefaultHistoryMaxGapMinutes = 30;
}

public sealed record CurrentSky(string Conditions, string Icon);
