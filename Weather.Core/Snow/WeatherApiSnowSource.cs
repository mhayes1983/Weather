using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>WeatherAPI.com's hourly <c>snow_cm</c>, <c>precip_in</c> and <c>gust_mph</c>. The free plan forecasts 3 days.</summary>
public sealed class WeatherApiSnowSource(
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : ISnowSource
{
    private const double CentimetresPerInch = 2.54;

    public ColumnDescriptor Column => SourceColumns.WeatherApi;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.WeatherApi.CacheMinutes);

    // The forecast has no snow depth.
    public IReadOnlyList<string> Views { get; } = [StormViews.Snow, StormViews.Rain, StormViews.Wind];

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
        var read = Read(await client.GetStringAsync(url, cancellationToken));
        return SnowResult.Ok(
            read.Snow,
            timeProvider.GetUtcNow(),
            liquid: read.Liquid.Count == 0 ? null : read.Liquid,
            gusts: read.Gusts.Count == 0 ? null : read.Gusts);
    }

    internal sealed record Series(List<SnowPeriod> Snow, List<SnowPeriod> Liquid, List<GustPeriod> Gusts);

    // Each hour's values are for the hour starting at its time_epoch: snow_cm, precip_in (all
    // precipitation as water) and gust_mph.
    internal static Series Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var series = new Series([], [], []);
        foreach (var day in document.RootElement.GetProperty("forecast").GetProperty("forecastday").EnumerateArray())
        {
            foreach (var hour in day.GetProperty("hour").EnumerateArray())
            {
                var start = DateTimeOffset.FromUnixTimeSeconds(hour.GetProperty("time_epoch").GetInt64());
                if (Number(hour, "snow_cm") is { } centimetres)
                {
                    var inches = centimetres / CentimetresPerInch;
                    series.Snow.Add(new SnowPeriod(start, start.AddHours(1), inches, inches));
                }

                if (Number(hour, "precip_in") is { } liquid)
                {
                    series.Liquid.Add(new SnowPeriod(start, start.AddHours(1), liquid, liquid));
                }

                if (Number(hour, "gust_mph") is { } mph)
                {
                    series.Gusts.Add(new GustPeriod(start, start.AddHours(1), mph));
                }
            }
        }

        return series;
    }

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
