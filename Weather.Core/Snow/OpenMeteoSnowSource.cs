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

    // Every series is asked for, though some models return no gusts or depth; while it works, the data decides.
    public IReadOnlyList<string> Views { get; } = [StormViews.Snow, StormViews.Rain, StormViews.Wind, StormViews.Depth];

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        // Times in GMT avoid clock-change gaps. Yesterday is included so a storm that began then still totals.
        // Every view's series comes in this one call, so switching views on the page costs nothing.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint}?latitude={location.Latitude}&longitude={location.Longitude}&hourly=snowfall,precipitation,wind_gusts_10m,snow_depth&precipitation_unit=inch&wind_speed_unit=mph&timezone=GMT&past_days=1&forecast_days={options.Value.ForecastDays}&models={string.Join(',', endpointModels)}");

        var json = await OpenMeteoRequests.GetSharedAsync(
            httpClientFactory, cache, $"om-snow:{endpoint}:{location.Zip}", url, CacheDuration, cancellationToken);
        var read = Read(json, model);
        return SnowResult.Ok(read.Snow, timeProvider.GetUtcNow(), liquid: read.Liquid, gusts: read.Gusts, depth: read.Depth);
    }

    private const double InchesPerFoot = 12;
    private const double InchesPerMetre = 39.3701;

    internal sealed record Series(
        List<SnowPeriod> Snow, List<SnowPeriod>? Liquid, List<GustPeriod>? Gusts, List<DepthPoint>? Depth);

    // With several models, each has its own {name}_{model} series. With one, it's just {name}.
    // Each amount is the hour before its time, and models that end early return null for the rest.
    // A model that doesn't forecast a series returns all nulls, which reads as null here.
    internal static Series Read(string json, string model)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var hourly = root.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var ends = new DateTimeOffset[times.GetArrayLength()];
        for (var i = 0; i < ends.Length; i++)
        {
            ends[i] = new DateTimeOffset(
                DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
                TimeSpan.Zero);
        }

        JsonElement? Find(string name) =>
            hourly.TryGetProperty($"{name}_{model}", out var series) ? series
            : hourly.TryGetProperty(name, out var single) ? single
            : null;

        List<T>? Values<T>(string name, Func<DateTimeOffset, double, T> make)
        {
            if (Find(name) is not { } series)
            {
                return null;
            }

            var list = new List<T>();
            for (var i = 0; i < ends.Length && i < series.GetArrayLength(); i++)
            {
                if (series[i].ValueKind == JsonValueKind.Number)
                {
                    list.Add(make(ends[i], series[i].GetDouble()));
                }
            }

            return list.Count == 0 ? null : list;
        }

        // Depth is in feet when the precipitation unit is inches, but the unit is read rather than assumed.
        var depthUnit = Find("snow_depth") is null ? null : Unit(root, $"snow_depth_{model}") ?? Unit(root, "snow_depth");
        var depthScale = depthUnit == "ft" ? InchesPerFoot : InchesPerMetre;

        return new Series(
            Values("snowfall", (end, inches) => new SnowPeriod(end.AddHours(-1), end, inches, inches)) ?? [],
            Values("precipitation", (end, inches) => new SnowPeriod(end.AddHours(-1), end, inches, inches)),
            Values("wind_gusts_10m", (end, mph) => new GustPeriod(end.AddHours(-1), end, mph)),
            Values("snow_depth", (time, depth) => new DepthPoint(time, Math.Round(depth * depthScale, 1))));
    }

    private static string? Unit(JsonElement root, string name) =>
        root.TryGetProperty("hourly_units", out var units) && units.TryGetProperty(name, out var unit) ? unit.GetString() : null;
}
