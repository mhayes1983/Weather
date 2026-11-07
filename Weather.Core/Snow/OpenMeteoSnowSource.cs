using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Snow;

/// <summary>
/// An Open-Meteo model's hourly snowfall, which is depth rather than melted liquid. Models on the same
/// endpoint share one request per town, because the Snow Forecast page asks for many towns at once and
/// Open-Meteo answers a burst of separate calls with 429.
/// </summary>
public sealed class OpenMeteoSnowSource(
    ColumnDescriptor column,
    string model,
    string endpoint,
    IReadOnlyList<string> endpointModels,
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : ISnowSource
{
    public ColumnDescriptor Column => column;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        // Times in GMT avoid clock-change gaps. Yesterday is included so a storm that began then still totals.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint}?latitude={location.Latitude}&longitude={location.Longitude}&hourly=snowfall&precipitation_unit=inch&timezone=GMT&past_days=1&forecast_days={options.Value.ForecastDays}&models={string.Join(',', endpointModels)}");

        var json = await OpenMeteoRequests.GetSharedAsync(
            httpClientFactory, cache, $"om-snow:{endpoint}:{location.Zip}", url, CacheDuration, cancellationToken);
        return SnowResult.Ok(Read(json, model), timeProvider.GetUtcNow());
    }

    // With several models, each has its own snowfall_{model} series. With one, it's just snowfall.
    // Each value is the hour before its time, and models that end early return null for the rest.
    internal static List<SnowPeriod> Read(string json, string model)
    {
        using var document = JsonDocument.Parse(json);
        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var values = hourly.TryGetProperty($"snowfall_{model}", out var series) ? series : hourly.GetProperty("snowfall");

        var periods = new List<SnowPeriod>();
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            if (values[i].ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var end = new DateTimeOffset(
                DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                TimeSpan.Zero);
            var inches = values[i].GetDouble();
            periods.Add(new SnowPeriod(end.AddHours(-1), end, inches, inches));
        }

        return periods;
    }
}
