using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>WeatherAPI.com's hourly <c>snow_cm</c>. The free plan forecasts 3 days.</summary>
public sealed class WeatherApiSnowSource(
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : ISnowSource
{
    private const double CentimetresPerInch = 2.54;

    public ColumnDescriptor Column { get; } = new("weatherapi", "WeatherAPI");

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.WeatherApi.CacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var settings = options.Value.WeatherApi;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return SnowResult.NeedsKey();
        }

        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.weatherapi.com/v1/forecast.json?key={Uri.EscapeDataString(settings.ApiKey)}&q={location.Latitude:0.####},{location.Longitude:0.####}&days={settings.ForecastDays}&aqi=no&alerts=no");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        return SnowResult.Ok(Read(await client.GetStringAsync(url, cancellationToken)), timeProvider.GetUtcNow());
    }

    // Each hour's snow_cm is the hour starting at its time_epoch.
    internal static List<SnowPeriod> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var periods = new List<SnowPeriod>();
        foreach (var day in document.RootElement.GetProperty("forecast").GetProperty("forecastday").EnumerateArray())
        {
            foreach (var hour in day.GetProperty("hour").EnumerateArray())
            {
                if (!hour.TryGetProperty("snow_cm", out var snow) || snow.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var start = DateTimeOffset.FromUnixTimeSeconds(hour.GetProperty("time_epoch").GetInt64());
                var inches = snow.GetDouble() / CentimetresPerInch;
                periods.Add(new SnowPeriod(start, start.AddHours(1), inches, inches));
            }
        }

        return periods;
    }
}
