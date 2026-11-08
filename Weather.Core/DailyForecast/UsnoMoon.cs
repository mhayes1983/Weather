using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;

namespace Weather.Core.DailyForecast;

public sealed class UsnoMoon(IHttpClientFactory httpClientFactory)
{
    private const string Endpoint = "https://aa.usno.navy.mil/api/rstt/oneday";

    /// <summary>The sun's and moon's rise and set for one date, and the moon's phase.</summary>
    public async Task<UsnoDay> GetAsync(GeoPoint location, DateOnly date, string timeZoneId, CancellationToken cancellationToken)
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
        var (moonrise, moonset) = RiseAndSet(data, "moondata");
        var (sunrise, sunset) = RiseAndSet(data, "sundata");

        return new UsnoDay(
            sunrise,
            sunset,
            new MoonInfo(
                data.TryGetProperty("curphase", out var phase) ? phase.GetString() : null,
                ReadPercent(data),
                moonrise,
                moonset));
    }

    private static (string? Rise, string? Set) RiseAndSet(JsonElement data, string property)
    {
        string? rise = null;
        string? set = null;
        if (data.TryGetProperty(property, out var events))
        {
            foreach (var skyEvent in events.EnumerateArray())
            {
                var time = skyEvent.GetProperty("time").GetString();
                switch (skyEvent.GetProperty("phen").GetString())
                {
                    case "Rise":
                        rise = time;
                        break;
                    case "Set":
                        set = time;
                        break;
                }
            }
        }

        return (rise, set);
    }

    private static int? ReadPercent(JsonElement data) =>
        data.TryGetProperty("fracillum", out var value)
        && int.TryParse(value.GetString()?.TrimEnd('%'), NumberStyles.None, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : null;
}
