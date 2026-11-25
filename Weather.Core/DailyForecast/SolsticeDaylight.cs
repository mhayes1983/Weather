using System.Globalization;
using System.Text.Json;
using Weather.Core.Models;

namespace Weather.Core.DailyForecast;

/// <summary>The longest or shortest daylight around a solstice, in minutes, or null where the sun doesn't rise or set.</summary>
public sealed record SolsticeReference(int? DaylightMinutes);

/// <summary>
/// Daylight around the last solstice, from Open-Meteo's archive, which has sunrise and sunset for any past date
/// through today, worked out the same way as the forecast's. The forecast endpoint only reaches back about 3 months.
/// </summary>
public sealed class SolsticeDaylight(IHttpClientFactory httpClientFactory)
{
    private const string ArchiveEndpoint = "https://archive-api.open-meteo.com/v1/archive";

    // In US time zones the solstices fall on June 20 or 21 and December 21 or 22, so a few days either side
    // always hold the turning point. Daylight barely changes there, so the exact day doesn't matter.
    private const int WindowDays = 2;

    /// <summary>
    /// The last solstice on or before <paramref name="date"/>, taken as June 21 or December 21. Daylight shrinks
    /// from the summer one and grows from the winter one.
    /// </summary>
    public static DateOnly LastSolstice(DateOnly date)
    {
        var summer = new DateOnly(date.Year, 6, 21);
        var winter = new DateOnly(date.Year, 12, 21);
        return date >= winter ? winter : date >= summer ? summer : new DateOnly(date.Year - 1, 12, 21);
    }

    /// <summary>The days the archive is asked for: around the solstice, ending no later than today, which is its last day.</summary>
    public static (DateOnly Start, DateOnly End) Window(DateOnly solstice, DateOnly today)
    {
        var end = solstice.AddDays(WindowDays);
        return (solstice.AddDays(-WindowDays), end < today ? end : today);
    }

    /// <summary>
    /// The longest daylight around a June solstice, or the shortest around a December one, from
    /// <paramref name="start"/> to <paramref name="end"/>. Its minutes are null when the sun doesn't rise or set there.
    /// </summary>
    public async Task<SolsticeReference> GetAsync(GeoPoint location, DateOnly solstice, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        // timezone=auto applies today's UTC offset to every date, which moves sunrise and sunset alike, so daylight is right.
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{ArchiveEndpoint}?latitude={location.Latitude}&longitude={location.Longitude}&timezone=auto&daily=sunrise,sunset&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        var daily = document.RootElement.GetProperty("daily");
        var sunrises = daily.GetProperty("sunrise");
        var sunsets = daily.GetProperty("sunset");

        var minutes = new List<int>();
        for (var i = 0; i < sunrises.GetArrayLength() && i < sunsets.GetArrayLength(); i++)
        {
            if (DailyForecastService.DaylightMinutes(Clock(sunrises[i]), Clock(sunsets[i])) is int daylight)
            {
                minutes.Add(daylight);
            }
        }

        if (minutes.Count == 0)
        {
            return new SolsticeReference(null);
        }

        return new SolsticeReference(solstice.Month == 6 ? minutes.Max() : minutes.Min());
    }

    // "2026-06-21T04:57" as "04:57".
    private static string? Clock(JsonElement time) =>
        time.ValueKind == JsonValueKind.String && time.GetString() is { } text ? text[(text.IndexOf('T') + 1)..] : null;
}
