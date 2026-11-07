using Weather.Core.Models;

namespace Weather.Core;

public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    public ForecastLocation DefaultLocation { get; set; } = new();
    public HomeStationSource HomeStationSource { get; set; } = HomeStationSource.AmbientWeather;
    public int AboveAverageF { get; set; } = 6;
    public int BelowAverageF { get; set; } = 6;
    public int ForecastDays { get; set; } = 16;
    public int SourceTimeoutSeconds { get; set; } = 15;
    public int FreeSourceCacheMinutes { get; set; } = 60;
    public int ErrorRetryMinutes { get; set; } = 5;
    public int AlertCacheMinutes { get; set; } = 2;
    public string UserAgent { get; set; } = "WeatherTable/1.0 (personal forecast comparison)";
    public KeyedSourceOptions WeatherUnderground { get; set; } = new() { ForecastDays = 5, CacheMinutes = 60 };

    /// <summary>WeatherAPI.com, used for snow only. Its free plan forecasts 3 days.</summary>
    public KeyedSourceOptions WeatherApi { get; set; } = new() { ForecastDays = 3, CacheMinutes = 60 };

    /// <summary>The towns the Snow Forecast page starts with, in column order.</summary>
    public List<SnowTownOptions> SnowTowns { get; set; } = [];
    public AmbientWeatherOptions AmbientWeather { get; set; } = new();
    public List<WeatherStationOptions> Stations { get; set; } = [];
    public WeatherUndergroundStationOptions WeatherUndergroundStations { get; set; } = new();
    public OpenMeteoStationOptions OpenMeteoStations { get; set; } = new();
}

public enum HomeStationSource
{
    AmbientWeather,
    OpenMeteo,
}

public sealed class WeatherStationOptions
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }

    /// <summary>Fills the Temperature Forecast ZIP box when this is the current station.</summary>
    public string? Zip { get; set; }
}

public sealed class SnowTownOptions
{
    public string Name { get; set; } = "";
    public string Zip { get; set; } = "";
}

public sealed class WeatherUndergroundStationOptions
{
    public int CurrentCacheMinutes { get; set; } = 5;
    public int HistoryCacheMinutes { get; set; } = 15;
    public int OfflineAfterMinutes { get; set; } = 30;
}

public sealed class OpenMeteoStationOptions
{
    public int CurrentCacheMinutes { get; set; } = 15;
    public int HistoryCacheMinutes { get; set; } = 60;
    public int OfflineAfterMinutes { get; set; } = 180;
    public int SkyTimeoutMilliseconds { get; set; } = 3000;
}

public sealed class AmbientWeatherOptions
{
    public string? ApiKey { get; set; }
    public string? ApplicationKey { get; set; }
    public string? MacAddress { get; set; }
    public string Name { get; set; } = "Home station";
    public int CurrentCacheSeconds { get; set; } = 60;
    public int HistoryCacheMinutes { get; set; } = 5;
    public int OfflineAfterMinutes { get; set; } = 15;
    public int RequestSpacingMilliseconds { get; set; } = 1100;
}

public sealed class KeyedSourceOptions
{
    public string? ApiKey { get; set; }
    public int ForecastDays { get; set; }
    public int CacheMinutes { get; set; }
}
