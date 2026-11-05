using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Sources;

public sealed class OpenMeteoSource(
    ColumnDescriptor column,
    string model,
    string endpoint,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider) : IForecastSource
{
    public const string ForecastEndpoint = "https://api.open-meteo.com/v1/forecast";
    public const string EnsembleEndpoint = "https://ensemble-api.open-meteo.com/v1/ensemble";

    public ColumnDescriptor Column => column;

    public TimeSpan CacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    public bool AppliesTo(ForecastLocation location) => true;

    public async Task<SourceResult> GetDailyTemperaturesAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint}?latitude={location.Latitude}&longitude={location.Longitude}&daily=temperature_2m_max,temperature_2m_min&temperature_unit=fahrenheit&timezone={Uri.EscapeDataString(location.TimeZoneId)}&forecast_days={options.Value.ForecastDays}&models={model}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var daily = document.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time");
        var highs = daily.GetProperty("temperature_2m_max");
        // Lows are optional, so a model without them still shows its highs.
        var hasLows = daily.TryGetProperty("temperature_2m_min", out var lows);

        var results = new List<DailyTemperatures>();
        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            // Models that end before the requested range return null for the remaining days.
            var high = ToWholeDegrees(highs[i]);
            var low = hasLows ? ToWholeDegrees(lows[i]) : null;
            if (high is not null || low is not null)
            {
                results.Add(new DailyTemperatures(
                    DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    high,
                    low));
            }
        }

        return SourceResult.Ok(results, timeProvider.GetUtcNow());
    }

    private static int? ToWholeDegrees(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number ? Temperature.ToWholeDegrees(value.GetDouble()) : null;
}
