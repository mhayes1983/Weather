using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Climate;
using Weather.Core.DailyForecast;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Almanac;

/// <summary>Yesterday's measurements and the forecast for today and tomorrow, beside a nearby climate station's averages and records.</summary>
public sealed class AlmanacService(
    StationDirectory directory,
    StationLocator locator,
    AcisAlmanac almanac,
    DailyForecastService forecasts,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<AlmanacService> logger)
{
    // Long enough to spare ACIS, short enough that a late report from yesterday shows up the same morning.
    private static readonly TimeSpan ClimateCacheDuration = TimeSpan.FromHours(1);

    /// <summary>Returns null when the ID is badly formed.</summary>
    public async Task<AlmanacReport?> GetAsync(string? stationId = null)
    {
        if (directory.Find(stationId) is not { } station)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            return new AlmanacReport(station.Name, SourceStatus.NeedsKey, "Needs key", null, null, null, []);
        }

        // The forecast knows the station's time zone. Without it, the home time zone is close enough for the date.
        var forecast = await forecasts.GetAsync(station.Id);
        var today = forecast?.Today ?? HomeToday();
        var (climate, message) = await GetClimateAsync(station, today);
        if (climate is null)
        {
            return new AlmanacReport(station.Name, SourceStatus.Error, message, null, null, null, []);
        }

        var sky = await forecasts.GetSkyAsync(station.Id, climate.Days.Select(day => day.Date).ToList());
        var days = climate.Days
            .Select(day => (day.Date < today ? day : WithForecast(day, forecast)) with { Sky = sky.GetValueOrDefault(day.Date) })
            .ToList();
        return new AlmanacReport(station.Name, SourceStatus.Ok, null, climate.Station, climate.RecordsSince, today, days);
    }

    // Today and later show the forecast. A failed forecast leaves it blank rather than hiding the averages and records.
    private static AlmanacDay WithForecast(AlmanacDay day, DailyForecastReport? forecast)
    {
        var forecastDay = forecast?.Days.FirstOrDefault(forecastDay => forecastDay.Date == day.Date);
        return day with
        {
            High = day.High with { Value = forecastDay?.HighF },
            Low = day.Low with { Value = forecastDay?.LowF },
            Precip = day.Precip with { Value = forecastDay?.PrecipIn },
            Snow = day.Snow with { Value = forecastDay?.SnowIn },
        };
    }

    private DateOnly HomeToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(options.Value.DefaultLocation.TimeZoneId)).DateTime);

    private Task<Fetched> GetClimateAsync(IWeatherStation station, DateOnly today) =>
        cache.GetOrCreateAsync(
            $"almanac:{station.Id}:{today:yyyy-MM-dd}",
            async () =>
            {
                try
                {
                    // Each step has its own timeout, so a slow location lookup can't use up the ACIS calls' time.
                    var location = await Climate.Acis.WithTimeoutAsync(
                        TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds),
                        token => locator.GetAsync(station, token));
                    return (new Fetched(await almanac.GetAsync(location, today), null), ClimateCacheDuration);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Almanac for {Id} failed: {Error}", station.Id, exception.GetType().Name);

                    // ClimateStations already remembers there's no station for a day, so asking again is cheap.
                    return (new Fetched(null, SourceFailure.Describe(exception)), TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private sealed record Fetched(ClimateAlmanac? Almanac, string? Message);
}
