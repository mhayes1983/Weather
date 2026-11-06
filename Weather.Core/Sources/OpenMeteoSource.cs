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

    // The daily min and max cover midnight to midnight, so a front can put a day's "high" at 2am or its
    // "low" late that night. Hours give the afternoon high and the morning low people mean instead.
    private const int HighStartHour = 9;
    private const int HighHours = 10; // 9am to 7pm
    private const int LowStartHour = 19;
    private const int LowHours = 14; // 7pm the evening before to 9am

    public async Task<SourceResult> GetDailyTemperaturesAsync(ForecastLocation location, CancellationToken cancellationToken)
    {
        // The day before is included so today's morning low has its evening hours.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{endpoint}?latitude={location.Latitude}&longitude={location.Longitude}&hourly=temperature_2m&temperature_unit=fahrenheit&timezone={Uri.EscapeDataString(location.TimeZoneId)}&past_days=1&forecast_days={options.Value.ForecastDays}&models={model}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var values = hourly.GetProperty("temperature_2m");

        // Models that end before the requested range return null for the remaining hours.
        var temperatures = new Dictionary<DateTime, double?>();
        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            var time = DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            temperatures[time] = values[i].ValueKind == JsonValueKind.Number ? values[i].GetDouble() : null;
        }

        var results = new List<DailyTemperatures>();
        foreach (var date in temperatures.Keys.Select(DateOnly.FromDateTime).Distinct())
        {
            var high = Extreme(temperatures, date.ToDateTime(new TimeOnly(HighStartHour, 0)), HighHours, Enumerable.Max);
            var low = Extreme(temperatures, date.AddDays(-1).ToDateTime(new TimeOnly(LowStartHour, 0)), LowHours, Enumerable.Min);
            if (high is not null || low is not null)
            {
                results.Add(new DailyTemperatures(date, high, low));
            }
        }

        return SourceResult.Ok(results, timeProvider.GetUtcNow());
    }

    // A window needs every hour, so a model that ends partway through it doesn't give a partial value.
    // One missing hour is allowed, for the hour a spring clock change skips.
    private static int? Extreme(
        Dictionary<DateTime, double?> temperatures, DateTime start, int hours, Func<IEnumerable<double>, double> pick)
    {
        var window = Enumerable.Range(0, hours)
            .Select(hour => start.AddHours(hour))
            .Where(temperatures.ContainsKey)
            .Select(time => temperatures[time])
            .ToList();

        return window.Count >= hours - 1 && window.All(value => value is not null)
            ? Temperature.ToWholeDegrees(pick(window.Select(value => value!.Value)))
            : null;
    }
}
