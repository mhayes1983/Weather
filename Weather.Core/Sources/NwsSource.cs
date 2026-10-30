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

    public async Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        var forecastUrl = await cache.GetOrCreateAsync(
            $"nws-grid:{location.Zip}",
            async () => (await GetForecastUrlAsync(client, location, cancellationToken), GridCacheDuration));

        using var document = JsonDocument.Parse(await client.GetStringAsync(forecastUrl, cancellationToken));

        var results = new List<DailyHigh>();
        foreach (var period in document.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray())
        {
            // Overnight periods hold lows, so an evening forecast has no high for today.
            if (period.GetProperty("isDaytime").GetBoolean())
            {
                var start = DateTimeOffset.Parse(period.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
                results.Add(new DailyHigh(DateOnly.FromDateTime(start.DateTime), period.GetProperty("temperature").GetInt32()));
            }
        }

        return SourceResult.Ok(results, timeProvider.GetUtcNow());
    }

    private static async Task<string> GetForecastUrlAsync(
        HttpClient client, ForecastLocation location, CancellationToken cancellationToken)
    {
        // NWS redirects requests with more than four decimal places.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.weather.gov/points/{location.Latitude:0.####},{location.Longitude:0.####}");

        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        return document.RootElement.GetProperty("properties").GetProperty("forecast").GetString()!;
    }
}
