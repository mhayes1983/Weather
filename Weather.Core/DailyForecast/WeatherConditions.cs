namespace Weather.Core.DailyForecast;

/// <summary>Turns WMO weather codes into a label and an icon key for the client.</summary>
public static class WeatherConditions
{
    public static (string Label, string Icon) Describe(int? code, bool isDay = true) => code switch
    {
        0 => isDay ? ("Sunny", "clear") : ("Clear", "clear-night"),
        1 => isDay ? ("Mostly Sunny", "mostly-clear") : ("Mostly Clear", "mostly-clear-night"),
        2 => ("Partly Cloudy", isDay ? "partly-cloudy" : "partly-cloudy-night"),
        3 => ("Cloudy", "cloudy"),
        45 or 48 => ("Fog", "fog"),
        51 or 53 or 55 => ("Drizzle", "drizzle"),
        56 or 57 => ("Freezing Drizzle", "sleet"),
        61 => ("Light Rain", "rain"),
        63 => ("Rain", "rain"),
        65 => ("Heavy Rain", "rain"),
        66 or 67 => ("Freezing Rain", "sleet"),
        71 => ("Light Snow", "snow"),
        73 => ("Snow", "snow"),
        75 => ("Heavy Snow", "snow"),
        77 => ("Snow Grains", "snow"),
        80 or 81 => ("Showers", "rain"),
        82 => ("Heavy Showers", "rain"),
        85 or 86 => ("Snow Showers", "snow"),
        95 => ("Thunderstorms", "storm"),
        96 or 99 => ("Thunderstorms with Hail", "storm"),
        _ => ("Unknown", "unknown"),
    };
}
