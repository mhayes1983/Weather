using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.DailyForecast;

public sealed record Outlook(
    string TimeZoneId,
    IReadOnlyList<ForecastDay> Days,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<ForecastHour>> Hours);

public sealed class OpenMeteoOutlook(IHttpClientFactory httpClientFactory, IOptions<WeatherOptions> options)
{
    private const double HectopascalsPerInHg = 33.8639;

    // WMO codes 0 to 3 run from clear to overcast. Anything higher is fog, rain, snow or storms.
    private const int LastDryCode = 3;

    private const string DailyFields = "weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,precipitation_sum,snowfall_sum,precipitation_probability_max,wind_speed_10m_max,wind_direction_10m_dominant,uv_index_max";
    private const string HourlyFields = "weather_code,is_day,temperature_2m,apparent_temperature,precipitation_probability,precipitation,snowfall,cloud_cover,dew_point_2m,relative_humidity_2m,wind_speed_10m,wind_direction_10m,pressure_msl";

    public async Task<Outlook> GetAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{OpenMeteoSource.ForecastEndpoint}?latitude={location.Latitude}&longitude={location.Longitude}&timezone=auto&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch&forecast_days={options.Value.ForecastDays}&daily={DailyFields}&hourly={HourlyFields}");

        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));

        var root = document.RootElement;
        var timeZoneId = root.GetProperty("timezone").GetString()!;
        var clock = new LocalClock(
            TimeSpan.FromSeconds(root.GetProperty("utc_offset_seconds").GetInt32()),
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

        var hourly = root.GetProperty("hourly");
        var hours = ReadHours(hourly, clock);
        return new Outlook(timeZoneId, ReadDays(root.GetProperty("daily"), hours, ReadDaylightCodes(hourly, clock), clock), hours);
    }

    private static Dictionary<DateOnly, List<int>> ReadDaylightCodes(JsonElement hourly, LocalClock clock)
    {
        var times = hourly.GetProperty("time");
        var codes = new Dictionary<DateOnly, List<int>>();

        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            if (WholeNumber(hourly, "is_day", i) == 0 || WholeNumber(hourly, "weather_code", i) is not int code)
            {
                continue;
            }

            var date = DateOnly.ParseExact(clock.Correct(times[i].GetString())![..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!codes.TryGetValue(date, out var list))
            {
                codes[date] = list = [];
            }

            list.Add(code);
        }

        return codes;
    }

    // The daily code is the day's worst hour, which suits rain and snow. On a dry day it
    // lets one cloudy hour before dawn label a sunny day, so those use the typical daylight hour.
    private static int? DayCode(int? dailyCode, IReadOnlyList<int>? daylightCodes) =>
        dailyCode is <= LastDryCode && daylightCodes is { Count: > 0 }
            ? daylightCodes
                .GroupBy(code => code)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key)
                .First().Key
            : dailyCode;

    private static List<ForecastDay> ReadDays(
        JsonElement daily,
        Dictionary<DateOnly, IReadOnlyList<ForecastHour>> hours,
        Dictionary<DateOnly, List<int>> daylightCodes,
        LocalClock clock)
    {
        var dates = daily.GetProperty("time");
        var days = new List<ForecastDay>();

        for (var i = 0; i < dates.GetArrayLength(); i++)
        {
            var date = DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var (conditions, icon) = WeatherConditions.Describe(
                DayCode(WholeNumber(daily, "weather_code", i), daylightCodes.GetValueOrDefault(date)));

            days.Add(new ForecastDay(
                date,
                conditions,
                icon,
                WholeNumber(daily, "temperature_2m_max", i),
                WholeNumber(daily, "temperature_2m_min", i),
                WholeNumber(daily, "precipitation_probability_max", i),
                Number(daily, "precipitation_sum", i),
                Number(daily, "snowfall_sum", i),
                Number(daily, "wind_speed_10m_max", i),
                WholeNumber(daily, "wind_direction_10m_dominant", i),
                Number(daily, "uv_index_max", i),
                clock.Correct(Text(daily, "sunrise", i)),
                clock.Correct(Text(daily, "sunset", i)),
                hours.ContainsKey(date)));
        }

        return days;
    }

    private static Dictionary<DateOnly, IReadOnlyList<ForecastHour>> ReadHours(JsonElement hourly, LocalClock clock)
    {
        var times = hourly.GetProperty("time");
        var hours = new Dictionary<DateOnly, List<ForecastHour>>();

        for (var i = 0; i < times.GetArrayLength(); i++)
        {
            // Hours past the end of the model's range come back as nulls.
            var temperature = WholeNumber(hourly, "temperature_2m", i);
            if (temperature is null)
            {
                continue;
            }

            var time = clock.Correct(times[i].GetString())!;
            var (conditions, icon) = WeatherConditions.Describe(
                WholeNumber(hourly, "weather_code", i),
                WholeNumber(hourly, "is_day", i) != 0);

            var date = DateOnly.ParseExact(time[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (!hours.TryGetValue(date, out var list))
            {
                hours[date] = list = [];
            }

            list.Add(new ForecastHour(
                time,
                conditions,
                icon,
                temperature,
                WholeNumber(hourly, "apparent_temperature", i),
                WholeNumber(hourly, "precipitation_probability", i),
                Number(hourly, "precipitation", i),
                Number(hourly, "snowfall", i),
                WholeNumber(hourly, "cloud_cover", i),
                WholeNumber(hourly, "dew_point_2m", i),
                WholeNumber(hourly, "relative_humidity_2m", i),
                Number(hourly, "wind_speed_10m", i),
                WholeNumber(hourly, "wind_direction_10m", i),
                Number(hourly, "pressure_msl", i) is double pressure
                    ? Math.Round(pressure / HectopascalsPerInHg, 2)
                    : null));
        }

        return hours.ToDictionary(pair => pair.Key, IReadOnlyList<ForecastHour> (pair) => pair.Value);
    }

    // Open-Meteo applies today's UTC offset to the whole range, so times after a clock change are an hour out.
    private sealed record LocalClock(TimeSpan ResponseOffset, TimeZoneInfo Zone)
    {
        private const string Format = "yyyy-MM-ddTHH:mm";

        public string? Correct(string? time)
        {
            if (time is null)
            {
                return null;
            }

            var moment = new DateTimeOffset(
                DateTime.ParseExact(time, Format, CultureInfo.InvariantCulture),
                ResponseOffset);
            return TimeZoneInfo.ConvertTime(moment, Zone).ToString(Format, CultureInfo.InvariantCulture);
        }
    }

    private static double? Number(JsonElement section, string name, int index)
    {
        if (!section.TryGetProperty(name, out var values) || index >= values.GetArrayLength())
        {
            return null;
        }

        return values[index].ValueKind == JsonValueKind.Number ? values[index].GetDouble() : null;
    }

    private static int? WholeNumber(JsonElement section, string name, int index) =>
        Number(section, name, index) is double value ? Temperature.ToWholeDegrees(value) : null;

    private static string? Text(JsonElement section, string name, int index) =>
        section.TryGetProperty(name, out var values)
        && index < values.GetArrayLength()
        && values[index].ValueKind == JsonValueKind.String
            ? values[index].GetString()
            : null;
}
