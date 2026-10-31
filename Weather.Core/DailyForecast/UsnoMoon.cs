using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;

namespace Weather.Core.DailyForecast;

public sealed class UsnoMoon(IHttpClientFactory httpClientFactory)
{
    private const string Endpoint = "https://aa.usno.navy.mil/api/rstt/oneday";

    public async Task<MoonInfo> GetAsync(GeoPoint location, DateOnly date, string timeZoneId, CancellationToken cancellationToken)
    {
        // The offset is for this date, because a forecast can cross a clock change.
        var offset = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)
            .GetUtcOffset(date.ToDateTime(new TimeOnly(12, 0)));

        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{Endpoint}?date={date:yyyy-MM-dd}&coords={location.Latitude:0.##},{location.Longitude:0.##}&tz={offset.TotalHours}&dst=false");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        var data = document.RootElement.GetProperty("properties").GetProperty("data");

        // The moon doesn't rise or set every day, so either event can be missing.
        string? moonrise = null;
        string? moonset = null;
        foreach (var moonEvent in data.GetProperty("moondata").EnumerateArray())
        {
            var time = moonEvent.GetProperty("time").GetString();
            switch (moonEvent.GetProperty("phen").GetString())
            {
                case "Rise":
                    moonrise = time;
                    break;
                case "Set":
                    moonset = time;
                    break;
            }
        }

        return new MoonInfo(
            data.TryGetProperty("curphase", out var phase) ? phase.GetString() : null,
            ReadPercent(data),
            moonrise,
            moonset);
    }

    private static int? ReadPercent(JsonElement data) =>
        data.TryGetProperty("fracillum", out var value)
        && int.TryParse(value.GetString()?.TrimEnd('%'), NumberStyles.None, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : null;
}
