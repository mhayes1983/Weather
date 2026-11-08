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

public sealed record StationSummary(string Id, string Name, string? Zip = null);

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
    public double? WindSpeedAvg2mMph { get; init; }
    public double? WindDirectionAvg2mDegrees { get; init; }
    public double? WindSpeedAvg10mMph { get; init; }
    public double? WindDirectionAvg10mDegrees { get; init; }
    public double? WindGustDirectionDegrees { get; init; }
    public double? Rain24HourIn { get; init; }
    public double? TotalRainIn { get; init; }

    /// <summary>Add-on sensors, listed only when the station reports them.</summary>
    public IReadOnlyList<ExtraSensor> ExtraSensors { get; init; } = [];
    public IReadOnlyList<SoilSensor> SoilSensors { get; init; } = [];
    public IReadOnlyList<LeakSensor> LeakSensors { get; init; } = [];
    public AirQuality? AirQuality { get; init; }
    public Lightning? Lightning { get; init; }

    /// <summary>Names of other sensors (indoor, rain gauge, lightning, CO2) whose batteries are low.</summary>
    public IReadOnlyList<string> LowBatteries { get; init; } = [];
}

public sealed record ExtraSensor(int Channel, double? TemperatureF, double? HumidityPercent, bool? BatteryLow);

public sealed record SoilSensor(int Channel, double? TemperatureF, double? MoisturePercent);

public sealed record LeakSensor(int Channel, bool Leak);

public sealed record AirQuality(double? Pm25, double? Pm25Avg24h, double? Aqi, double? IndoorPm25, double? IndoorCo2Ppm);

public sealed record Lightning(double? StrikesToday, double? StrikesLastHour, DateTimeOffset? LastStrike, double? LastDistanceMi);

public sealed record StationHistoryPoint(
    DateTimeOffset Time,
    double? TemperatureF,
    double? WindSpeedMph,
    double? WindGustMph,
    double? PressureInHg,
    double? RainRateInPerHour,
    double? HumidityPercent = null,
    double? DewPointF = null,
    double? UvIndex = null,
    double? SolarRadiationWm2 = null,
    double? IndoorTemperatureF = null);

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
