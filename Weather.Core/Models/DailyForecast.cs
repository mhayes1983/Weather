namespace Weather.Core.Models;

public sealed record GeoPoint(double Latitude, double Longitude);

public sealed record ForecastDay(
    DateOnly Date,
    string Conditions,
    string Icon,
    int? HighF,
    int? LowF,
    int? PrecipChancePercent,
    double? PrecipIn,
    double? SnowIn,
    double? WindMph,
    int? WindDirectionDegrees,
    double? UvIndex,
    string? Sunrise,
    string? Sunset,
    bool HasHourly);

public sealed record ForecastHour(
    string Time,
    string Conditions,
    string Icon,
    int? TemperatureF,
    int? FeelsLikeF,
    int? PrecipChancePercent,
    double? PrecipIn,
    double? SnowIn,
    int? CloudCoverPercent,
    int? DewPointF,
    int? HumidityPercent,
    double? WindMph,
    int? WindDirectionDegrees,
    double? PressureInHg);

public sealed record PeriodPrecip(int? ChancePercent, double? AmountIn, double? SnowIn);

public sealed record MoonInfo(string? Phase, int? IlluminationPercent, string? Moonrise, string? Moonset);

public sealed record DailyForecastReport(
    string StationName,
    SourceStatus Status,
    string? Message,
    DateOnly? Today,
    IReadOnlyList<ForecastDay> Days);

public sealed record ForecastDayDetail(
    string StationName,
    ForecastDay Day,
    string? DayDescription,
    string? NightDescription,
    PeriodPrecip? DayPrecip,
    PeriodPrecip? NightPrecip,
    MoonInfo? Moon,
    IReadOnlyList<ForecastHour> Hours,
    DateOnly? PreviousDate,
    DateOnly? NextDate);
