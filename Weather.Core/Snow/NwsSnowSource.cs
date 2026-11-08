using System.Globalization;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>
/// NWS snow: the forecast text's range ("1 to 3 inches") for each 12-hour period that gives one,
/// and the gridpoint <c>snowfallAmount</c> for the rest.
/// </summary>
public sealed class NwsSnowSource(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : ISnowSource
{
    private const double MillimetresPerInch = 25.4;
    private static readonly TimeSpan PointCacheDuration = TimeSpan.FromDays(30);

    public ColumnDescriptor Column => SourceColumns.Nws;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        var (forecastUrl, gridUrl) = await cache.GetOrCreateAsync(
            $"nws-points:{location.Zip}",
            async () => (await GetUrlsAsync(client, location, cancellationToken), PointCacheDuration));

        var textTask = client.GetStringAsync(forecastUrl, cancellationToken);
        var gridTask = client.GetStringAsync(gridUrl, cancellationToken);
        await Task.WhenAll(textTask, gridTask);

        var ranges = ReadTextRanges(await textTask);
        var periods = new List<SnowPeriod>(ranges);
        foreach (var amount in ReadGrid(await gridTask))
        {
            var middle = amount.Start + ((amount.End - amount.Start) / 2);
            if (!ranges.Any(range => range.Start <= middle && middle < range.End))
            {
                periods.Add(amount);
            }
        }

        return SnowResult.Ok(periods, timeProvider.GetUtcNow());
    }

    internal static List<SnowPeriod> ReadTextRanges(string json)
    {
        using var document = JsonDocument.Parse(json);
        var ranges = new List<SnowPeriod>();
        foreach (var period in document.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray())
        {
            if (SnowText.Read(period.TryGetProperty("detailedForecast", out var text) ? text.GetString() : null) is { } range)
            {
                ranges.Add(new SnowPeriod(
                    DateTimeOffset.Parse(period.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(period.GetProperty("endTime").GetString()!, CultureInfo.InvariantCulture),
                    range.Low,
                    range.High));
            }
        }

        return ranges;
    }

    // Values look like { "validTime": "2026-11-07T12:00:00+00:00/PT6H", "value": 25.4 } in millimetres.
    internal static List<SnowPeriod> ReadGrid(string json)
    {
        using var document = JsonDocument.Parse(json);
        var periods = new List<SnowPeriod>();
        if (!document.RootElement.GetProperty("properties").TryGetProperty("snowfallAmount", out var snowfall))
        {
            return periods;
        }

        foreach (var value in snowfall.GetProperty("values").EnumerateArray())
        {
            if (value.GetProperty("value").ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var parts = value.GetProperty("validTime").GetString()!.Split('/');
            var start = DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture);
            var inches = value.GetProperty("value").GetDouble() / MillimetresPerInch;
            periods.Add(new SnowPeriod(start, start + XmlConvert.ToTimeSpan(parts[1]), inches, inches));
        }

        return periods;
    }

    private static async Task<(string Forecast, string Grid)> GetUrlsAsync(
        HttpClient client, ForecastLocation location, CancellationToken cancellationToken)
    {
        // NWS redirects requests with more than four decimal places.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.weather.gov/points/{location.Latitude:0.####},{location.Longitude:0.####}");

        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        var properties = document.RootElement.GetProperty("properties");
        return (properties.GetProperty("forecast").GetString()!, properties.GetProperty("forecastGridData").GetString()!);
    }
}
