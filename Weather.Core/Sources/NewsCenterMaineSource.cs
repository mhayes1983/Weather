using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class NewsCenterMaineSource(
    ColumnDescriptor column,
    string section,
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    private const string PageUrl = "https://www.newscentermaine.com/10-day";

    // Both columns read the same page, so it is only downloaded once per refresh.
    private static readonly TimeSpan PageCacheDuration = TimeSpan.FromMinutes(5);

    public ColumnDescriptor Column => column;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => location.State == "ME";

    public async Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var html = await cache.GetOrCreateAsync(
            "ncm-page",
            async () =>
            {
                var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
                return (await client.GetStringAsync(PageUrl, cancellationToken), PageCacheDuration);
            });

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            now, TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId)).DateTime);

        return SourceResult.Ok(NewsCenterMaineParser.Parse(html, section, today), now);
    }
}
