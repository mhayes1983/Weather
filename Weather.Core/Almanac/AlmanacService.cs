using System.Globalization;
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
    NoaaTides noaaTides,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<AlmanacService> logger)
{
    // Long enough to spare ACIS, short enough that a late report from yesterday shows up the same morning.
    private static readonly TimeSpan ClimateCacheDuration = TimeSpan.FromHours(1);

    private static readonly TimeSpan TideStationCacheDuration = TimeSpan.FromDays(30);
    private static readonly TimeSpan TidesCacheDuration = TimeSpan.FromHours(24);

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

        // The sky and tides don't depend on the climate station, so none waits for another.
        var climateTask = GetClimateAsync(station, today);
        var skyTask = forecasts.GetSkyAsync(station.Id, [today.AddDays(-1), today, today.AddDays(1)]);
        var tidesTask = GetTidesAsync(station, today);
        await Task.WhenAll(climateTask, skyTask, tidesTask);

        var (climate, message) = climateTask.Result;
        if (climate is null)
        {
            return new AlmanacReport(station.Name, SourceStatus.Error, message, null, null, null, []);
        }

        var sky = skyTask.Result;
        var tides = tidesTask.Result;
        var days = climate.Days
            .Select(day => (day.Date < today ? day : WithForecast(day, forecast)) with
            {
                Sky = sky.GetValueOrDefault(day.Date),
                Tides = tides?.Tides.GetValueOrDefault(day.Date),
            })
            .ToList();
        return new AlmanacReport(station.Name, SourceStatus.Ok, null, climate.Station, climate.RecordsSince, today, days, tides?.Station.Name);
    }

    // Tides only add detail, so a failure hides them and nothing else, and is retried like the other sources.
    // Each step has its own timeout, like the ACIS calls.
    private Task<FetchedTides?> GetTidesAsync(IWeatherStation station, DateOnly today) =>
        cache.GetOrCreateAsync(
            $"almanac-tides:{station.Id}:{today:yyyy-MM-dd}",
            async () =>
            {
                var timeout = TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds);
                try
                {
                    var location = await Climate.Acis.WithTimeoutAsync(timeout, token => locator.GetAsync(station, token));

                    // Stations rarely change and the list is about 2 MB, so the nearest is kept for a month, as is "none nearby".
                    var tideStation = await cache.GetOrCreateAsync(
                        string.Create(CultureInfo.InvariantCulture, $"tide-station:{location.Latitude},{location.Longitude}"),
                        async () => (await Climate.Acis.WithTimeoutAsync(timeout, token => noaaTides.FindStationAsync(location, token)), TideStationCacheDuration));
                    // This entry's key changes daily, so it never outlives the day; the station entry above saves the list.
                    if (tideStation is null)
                    {
                        return ((FetchedTides?)null, TidesCacheDuration);
                    }

                    // Predictions don't change, so they're kept for the rest of the day.
                    var predictions = await Climate.Acis.WithTimeoutAsync(
                        timeout,
                        token => noaaTides.GetAsync(tideStation.Id, today.AddDays(-1), today.AddDays(1), token));
                    return (new FetchedTides(tideStation, predictions), TidesCacheDuration);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Tides for {Id} failed: {Error}", station.Id, exception.GetType().Name);
                    return (null, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    private sealed record FetchedTides(TideStation Station, IReadOnlyDictionary<DateOnly, IReadOnlyList<AlmanacTide>> Tides);

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
