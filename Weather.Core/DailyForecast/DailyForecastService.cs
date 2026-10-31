using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.DailyForecast;

public sealed class DailyForecastService(
    IEnumerable<IWeatherStation> stations,
    StationLocator locator,
    OpenMeteoOutlook outlook,
    NwsDescriptions descriptions,
    UsnoMoon moon,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<DailyForecastService> logger)
{
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MoonCacheDuration = TimeSpan.FromHours(24);

    private TimeSpan ForecastCacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    private TimeSpan ErrorRetryDuration => TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes);

    /// <summary>Returns null when the ID isn't a configured station.</summary>
    public async Task<DailyForecastReport?> GetAsync(string? stationId = null)
    {
        if (FindStation(stationId) is not { } station)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            return new DailyForecastReport(station.Name, SourceStatus.NeedsKey, "Needs key", []);
        }

        var (forecast, _, message) = await GetOutlookAsync(station);
        return new DailyForecastReport(
            station.Name,
            message is null ? SourceStatus.Ok : SourceStatus.Error,
            message,
            forecast?.Days ?? []);
    }

    /// <summary>Returns null when the station isn't configured or the day has no hourly data.</summary>
    public async Task<ForecastDayDetail?> GetDayAsync(DateOnly date, string? stationId = null)
    {
        if (FindStation(stationId) is not { } station || !station.CanLocate)
        {
            return null;
        }

        var (forecast, location, _) = await GetOutlookAsync(station);
        var day = forecast?.Days.FirstOrDefault(day => day.Date == date);
        if (day is not { HasHourly: true })
        {
            return null;
        }

        var descriptionsTask = GetDescriptionsAsync(location!);
        var moonTask = GetMoonAsync(location!, date, forecast!.TimeZoneId);
        await Task.WhenAll(descriptionsTask, moonTask);

        var description = descriptionsTask.Result?.GetValueOrDefault(date);
        var openable = forecast.Days.Where(other => other.HasHourly).ToList();
        return new ForecastDayDetail(
            station.Name,
            day,
            description?.Day,
            description?.Night,
            moonTask.Result,
            RemainingHours(forecast, date),
            openable.LastOrDefault(other => other.Date < date)?.Date,
            openable.FirstOrDefault(other => other.Date > date)?.Date);
    }

    private IWeatherStation? FindStation(string? id) =>
        stations.FirstOrDefault(station =>
            string.Equals(station.Id, id ?? AmbientWeatherStation.HomeId, StringComparison.OrdinalIgnoreCase));

    // Today only lists the current hour onwards.
    private List<ForecastHour> RemainingHours(Outlook forecast, DateOnly date)
    {
        var now = TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(forecast.TimeZoneId));
        var currentHour = now.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);

        return forecast.Hours[date]
            .Where(hour => string.CompareOrdinal(hour.Time, 0, currentHour, 0, currentHour.Length) >= 0)
            .ToList();
    }

    private Task<Fetched> GetOutlookAsync(IWeatherStation station) =>
        cache.GetOrCreateAsync(
            $"outlook:{station.Id}",
            async () =>
            {
                var lastGoodKey = $"last-good:outlook:{station.Id}";

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var location = await locator.GetAsync(station, timeout.Token);
                    var fetched = new Fetched(await outlook.GetAsync(location, timeout.Token), location, null);

                    cache.Set(lastGoodKey, fetched, LastGoodDuration);
                    return (fetched, ForecastCacheDuration);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Daily forecast for {Id} failed: {Error}", station.Id, exception.GetType().Name);

                    var message = SourceFailure.Describe(exception);
                    var fetched = cache.TryGet<Fetched>(lastGoodKey, out var lastGood)
                        ? lastGood! with { Message = $"Showing last good data. {message}" }
                        : new Fetched(null, null, message);
                    return (fetched, ErrorRetryDuration);
                }
            });

    private Task<IReadOnlyDictionary<DateOnly, DayDescription>?> GetDescriptionsAsync(GeoPoint location) =>
        GetOptionalAsync(
            string.Create(CultureInfo.InvariantCulture, $"descriptions:{location.Latitude},{location.Longitude}"),
            ForecastCacheDuration,
            async token => await descriptions.GetAsync(location, token));

    private Task<MoonInfo?> GetMoonAsync(GeoPoint location, DateOnly date, string timeZoneId) =>
        GetOptionalAsync(
            string.Create(CultureInfo.InvariantCulture, $"moon:{location.Latitude},{location.Longitude}:{date:yyyy-MM-dd}"),
            MoonCacheDuration,
            token => moon.GetAsync(location, date, timeZoneId, token));

    // These sources only add detail, so a failure hides their section and nothing else.
    private Task<T?> GetOptionalAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> fetch) where T : class =>
        cache.GetOrCreateAsync<T?>(
            key,
            async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    return (await fetch(timeout.Token), ttl);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("{Key} failed: {Error}", key, exception.GetType().Name);
                    return (null, ErrorRetryDuration);
                }
            });

    private sealed record Fetched(Outlook? Forecast, GeoPoint? Location, string? Message);
}
