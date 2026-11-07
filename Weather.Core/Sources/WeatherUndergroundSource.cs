using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class WeatherUndergroundSource(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    public ColumnDescriptor Column { get; } = new("wu", "WU");

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.WeatherUnderground.CacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SourceResult> GetDailyTemperaturesAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var settings = options.Value.WeatherUnderground;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return SourceResult.NeedsKey();
        }

        using var document = JsonDocument.Parse(await WeatherUndergroundForecast.GetJsonAsync(
            httpClientFactory, cache, settings, location.Zip, cancellationToken));

        // The calendar-day fields cover midnight to midnight, so the day parts give the afternoon high
        // and morning low instead. They alternate day and night for each date, and today's day part is
        // null once it's over.
        var dates = document.RootElement.GetProperty("validTimeLocal");
        var temperatures = document.RootElement.GetProperty("daypart")[0].GetProperty("temperature");

        var highs = new Dictionary<DateOnly, int>();
        var lows = new Dictionary<DateOnly, int>();
        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            // Dates look like 2026-10-30T07:00:00-0400.
            var date = DateOnly.ParseExact(dates[i].GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (temperatures[2 * i] is { ValueKind: JsonValueKind.Number } high)
            {
                highs[date] = high.GetInt32();
            }

            if (temperatures[(2 * i) + 1] is { ValueKind: JsonValueKind.Number } low)
            {
                lows[date.AddDays(1)] = low.GetInt32();
            }
        }

        return SourceResult.Ok(DayParts.Merge(highs, lows), timeProvider.GetUtcNow());
    }
}
