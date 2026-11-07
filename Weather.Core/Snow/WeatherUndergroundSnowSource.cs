using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Snow;

/// <summary>
/// Weather Underground's day parts: 7am to 7pm, then 7pm to 7am. Each has <c>qpfSnow</c> in inches,
/// and its narrative often gives a range ("Snow accumulations of 1 to 3 inches"), which is used first.
/// </summary>
public sealed class WeatherUndergroundSnowSource(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : ISnowSource
{
    private static readonly TimeSpan DayPart = TimeSpan.FromHours(12);

    public ColumnDescriptor Column { get; } = new("wu", "Weather Channel / WU");

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.WeatherUnderground.CacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var settings = options.Value.WeatherUnderground;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return SnowResult.NeedsKey();
        }

        var json = await WeatherUndergroundForecast.GetJsonAsync(httpClientFactory, cache, settings, location.Zip, cancellationToken);
        return SnowResult.Ok(Read(json), timeProvider.GetUtcNow());
    }

    internal static List<SnowPeriod> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var dates = document.RootElement.GetProperty("validTimeLocal");
        var daypart = document.RootElement.GetProperty("daypart")[0];
        var amounts = daypart.GetProperty("qpfSnow");
        var narratives = daypart.GetProperty("narrative");

        var periods = new List<SnowPeriod>();
        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            // Dates look like 2026-10-30T07:00:00-0400, the start of that date's day part.
            var text = dates[i].GetString()!;
            var dayStart = DateTimeOffset.Parse(text.Insert(text.Length - 2, ":"), CultureInfo.InvariantCulture);

            for (var part = 0; part < 2; part++)
            {
                // Today's day part is null once it's over.
                var index = (2 * i) + part;
                if (index >= amounts.GetArrayLength() || amounts[index].ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                var start = dayStart + (DayPart * part);
                var amount = amounts[index].GetDouble();
                var range = SnowText.Read(narratives[index].ValueKind == JsonValueKind.String ? narratives[index].GetString() : null);
                periods.Add(new SnowPeriod(start, start + DayPart, range?.Low ?? amount, range?.High ?? amount));
            }
        }

        return periods;
    }
}
