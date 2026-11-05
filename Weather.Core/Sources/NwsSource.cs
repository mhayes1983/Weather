using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class NwsSource(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    private static readonly TimeSpan GridCacheDuration = TimeSpan.FromDays(30);

    public ColumnDescriptor Column { get; } = new("nws", "NWS");

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SourceResult> GetDailyTemperaturesAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        var forecastUrl = await cache.GetOrCreateAsync(
            $"nws-grid:{location.Zip}",
            async () => (await GetForecastUrlAsync(client, location.Latitude, location.Longitude, cancellationToken), GridCacheDuration));

        using var document = JsonDocument.Parse(await client.GetStringAsync(forecastUrl, cancellationToken));

        var highs = new Dictionary<DateOnly, int>();
        var lows = new Dictionary<DateOnly, int>();
        foreach (var period in document.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray())
        {
            var start = DateTimeOffset.Parse(period.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
            var temperature = period.GetProperty("temperature").GetInt32();

            // Overnight periods hold lows, so an evening forecast has no high for today. A night's low is
            // the morning low of the date it ends on. Adding 12 hours moves an evening start to the next
            // date and keeps a start after midnight on its own date.
            if (period.GetProperty("isDaytime").GetBoolean())
            {
                highs.TryAdd(DateOnly.FromDateTime(start.DateTime), temperature);
            }
            else
            {
                lows.TryAdd(DateOnly.FromDateTime(start.DateTime.AddHours(12)), temperature);
            }
        }

        return SourceResult.Ok(DayParts.Merge(highs, lows), timeProvider.GetUtcNow());
    }

    internal static async Task<string> GetForecastUrlAsync(
        HttpClient client, double latitude, double longitude, CancellationToken cancellationToken)
    {
        // NWS redirects requests with more than four decimal places.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.weather.gov/points/{latitude:0.####},{longitude:0.####}");

        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        return document.RootElement.GetProperty("properties").GetProperty("forecast").GetString()!;
    }
}
