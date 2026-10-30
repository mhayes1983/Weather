using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class OpenMeteoSource(
    ColumnDescriptor column,
    string model,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    public ColumnDescriptor Column => column;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={location.Latitude}&longitude={location.Longitude}&daily=temperature_2m_max&temperature_unit=fahrenheit&timezone={Uri.EscapeDataString(location.TimeZoneId)}&forecast_days={options.Value.ForecastDays}&models={model}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var daily = document.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time");
        var highs = daily.GetProperty("temperature_2m_max");

        var results = new List<DailyHigh>();
        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            // Models that end before the requested range return null for the remaining days.
            if (highs[i].ValueKind == JsonValueKind.Number)
            {
                results.Add(new DailyHigh(
                    DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Temperature.ToWholeDegrees(highs[i].GetDouble())));
            }
        }

        return SourceResult.Ok(results, timeProvider.GetUtcNow());
    }
}
