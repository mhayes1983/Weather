using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.DailyForecast;

public sealed record DayDescription(string? Day, string? Night);

public sealed class NwsDescriptions(IHttpClientFactory httpClientFactory, WeatherCache cache)
{
    private const int LastHourOfPreviousNight = 6;

    public async Task<IReadOnlyDictionary<DateOnly, DayDescription>> GetAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        var forecastUrl = await NwsSource.GetCachedForecastUrlAsync(client, cache, location, cancellationToken);

        using var document = JsonDocument.Parse(await client.GetStringAsync(forecastUrl, cancellationToken));

        var descriptions = new Dictionary<DateOnly, DayDescription>();
        foreach (var period in document.RootElement.GetProperty("properties").GetProperty("periods").EnumerateArray())
        {
            var start = DateTimeOffset.Parse(period.GetProperty("startTime").GetString()!, CultureInfo.InvariantCulture);
            var text = period.GetProperty("detailedForecast").GetString();
            var isDaytime = period.GetProperty("isDaytime").GetBoolean();

            // A night period that starts after midnight is the rest of the previous night.
            var date = DateOnly.FromDateTime(start.DateTime);
            if (!isDaytime && start.Hour < LastHourOfPreviousNight)
            {
                date = date.AddDays(-1);
            }

            var current = descriptions.GetValueOrDefault(date) ?? new DayDescription(null, null);
            descriptions[date] = isDaytime ? current with { Day = text } : current with { Night = text };
        }

        return descriptions;
    }
}
