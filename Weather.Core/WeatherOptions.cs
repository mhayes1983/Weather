using Weather.Core.Models;

namespace Weather.Core;

public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    public ForecastLocation DefaultLocation { get; set; } = new();
    public int AboveAverageF { get; set; } = 6;
    public int BelowAverageF { get; set; } = 6;
    public int ForecastDays { get; set; } = 16;
    public int SourceTimeoutSeconds { get; set; } = 15;
    public int FreeSourceCacheMinutes { get; set; } = 30;
    public int ErrorRetryMinutes { get; set; } = 5;
    public string UserAgent { get; set; } = "WeatherTable/1.0 (personal forecast comparison)";
    public KeyedSourceOptions WeatherUnderground { get; set; } = new() { ForecastDays = 5, CacheMinutes = 60 };
}

public sealed class KeyedSourceOptions
{
    public string? ApiKey { get; set; }
    public int ForecastDays { get; set; }
    public int CacheMinutes { get; set; }
}
