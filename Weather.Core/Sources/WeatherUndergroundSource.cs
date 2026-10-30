using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class WeatherUndergroundSource(
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    public ColumnDescriptor Column { get; } = new("wu", "WU");

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.WeatherUnderground.CacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var settings = options.Value.WeatherUnderground;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return SourceResult.NeedsKey();
        }

        var url = $"https://api.weather.com/v3/wx/forecast/daily/{settings.ForecastDays}day?postalKey={location.Zip}:US&units=e&language=en-US&format=json&apiKey={Uri.EscapeDataString(settings.ApiKey)}";

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var dates = document.RootElement.GetProperty("validTimeLocal");
        var highs = document.RootElement.GetProperty("calendarDayTemperatureMax");

        var results = new List<DailyHigh>();
        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            if (highs[i].ValueKind == JsonValueKind.Number)
            {
                // Dates look like 2026-10-30T07:00:00-0400.
                var date = DateOnly.ParseExact(dates[i].GetString()![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                results.Add(new DailyHigh(date, highs[i].GetInt32()));
            }
        }

        return SourceResult.Ok(results, timeProvider.GetUtcNow());
    }
}
