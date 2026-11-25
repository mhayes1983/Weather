using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.DailyForecast;

public sealed class DailyForecastService(
    StationDirectory directory,
    StationLocator locator,
    OpenMeteoOutlook outlook,
    NwsDescriptions descriptions,
    NwsDiscussion discussion,
    UsnoMoon moon,
    SolsticeDaylight solsticeDaylight,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<DailyForecastService> logger)
{
    // NWS day periods run from 6am to 6pm, and night periods from 6pm to 6am.
    private const int DayStartHour = 6;
    private const int NightStartHour = 18;

    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MoonCacheDuration = TimeSpan.FromHours(24);

    // Past sunrise and sunset never change, and a solstice is used for six months.
    private static readonly TimeSpan SolsticeCacheDuration = TimeSpan.FromDays(30);

    private TimeSpan ForecastCacheDuration => TimeSpan.FromMinutes(options.Value.FreeSourceCacheMinutes);

    private TimeSpan ErrorRetryDuration => TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes);

    /// <summary>Returns null when the ID is badly formed.</summary>
    public async Task<DailyForecastReport?> GetAsync(string? stationId = null)
    {
        if (directory.Find(stationId) is not { } station)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            return new DailyForecastReport(station.Name, SourceStatus.NeedsKey, "Needs key", null, []);
        }

        var (forecast, _, message) = await GetOutlookAsync(station);
        return new DailyForecastReport(
            station.Name,
            message is null ? SourceStatus.Ok : SourceStatus.Error,
            message,
            forecast is null ? null : Today(forecast),
            forecast is null ? [] : RemainingDays(forecast));
    }

    /// <summary>Returns null when the ID is badly formed or the day can't be opened.</summary>
    /// <exception cref="ForecastUnavailableException">The forecast couldn't be loaded.</exception>
    public async Task<ForecastDayDetail?> GetDayAsync(DateOnly date, string? stationId = null)
    {
        if (directory.Find(stationId) is not { } station)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            throw new ForecastUnavailableException("Add your Weather Underground key to see this station's forecast.");
        }

        var (forecast, location, message) = await GetOutlookAsync(station);
        if (forecast is null)
        {
            throw new ForecastUnavailableException(message ?? "The forecast could not be loaded.");
        }

        var days = RemainingDays(forecast);
        var day = days.FirstOrDefault(day => day.Date == date);
        if (day is not { HasHourly: true })
        {
            return null;
        }

        var descriptionsTask = GetDescriptionsAsync(location!);
        var moonTask = GetUsnoAsync(location!, date, forecast.TimeZoneId);
        await Task.WhenAll(descriptionsTask, moonTask);

        var description = descriptionsTask.Result?.GetValueOrDefault(date);
        var openable = days.Where(other => other.HasHourly).ToList();
        var hours = RemainingHours(forecast, date);
        var nextDate = date.AddDays(1);
        var upcoming = hours.Concat(forecast.Hours.GetValueOrDefault(nextDate) ?? []).ToList();
        var morning = PeriodStart(date, DayStartHour);
        var evening = PeriodStart(date, NightStartHour);
        return new ForecastDayDetail(
            station.Name,
            day,
            description?.Day,
            description?.Night,
            description?.Day is null ? null : Precipitation(upcoming, morning, evening),
            description?.Night is null ? null : Precipitation(upcoming, evening, PeriodStart(nextDate, DayStartHour)),
            moonTask.Result?.Moon,
            hours,
            openable.LastOrDefault(other => other.Date < date)?.Date,
            openable.FirstOrDefault(other => other.Date > date)?.Date);
    }

    /// <summary>
    /// Sunrise, sunset and moon data for each date, from the forecast where it has the date and USNO otherwise.
    /// A failed USNO call leaves only the forecast's sun times, dates with neither are left out,
    /// and a failed forecast leaves out every date.
    /// </summary>
    public async Task<IReadOnlyDictionary<DateOnly, AlmanacSky>> GetSkyAsync(string? stationId, IReadOnlyList<DateOnly> dates)
    {
        if (directory.Find(stationId) is not { CanLocate: true } station)
        {
            return new Dictionary<DateOnly, AlmanacSky>();
        }

        var (forecast, location, _) = await GetOutlookAsync(station);
        if (forecast is null)
        {
            return new Dictionary<DateOnly, AlmanacSky>();
        }

        var usno = (await Task.WhenAll(dates.Select(date => GetUsnoAsync(location!, date, forecast.TimeZoneId))))
            .Select((day, i) => (Date: dates[i], Day: day))
            .ToDictionary(pair => pair.Date, pair => pair.Day);

        // The daily forecast shows Open-Meteo's times, so the almanac uses them too where it can. They keep
        // their dates here, because in Alaska the sun can set after midnight or not at all.
        (string? Sunrise, string? Sunset) Sun(DateOnly date)
        {
            var forecastDay = forecast.Days.FirstOrDefault(other => other.Date == date);
            var usnoDay = usno.GetValueOrDefault(date);
            return (forecastDay?.Sunrise ?? usnoDay?.Sunrise, forecastDay?.Sunset ?? usnoDay?.Sunset);
        }

        // The dates usually share one solstice, so this is one cached archive call.
        var today = Today(forecast);
        var solstices = dates.Select(SolsticeDaylight.LastSolstice).Distinct().ToList();
        var references = (await Task.WhenAll(solstices.Select(solstice => GetSolsticeAsync(location!, solstice, today))))
            .Select((reference, i) => (Solstice: solstices[i], Reference: reference))
            .ToDictionary(pair => pair.Solstice, pair => pair.Reference);

        var sky = new Dictionary<DateOnly, AlmanacSky>();
        foreach (var date in dates)
        {
            var day = usno[date];
            var (sunrise, sunset) = Sun(date);
            if (day is null && sunrise is null && sunset is null)
            {
                continue;
            }

            // The day before the first date comes from the forecast's past days, so it costs no USNO call.
            var daylight = DaylightMinutes(sunrise, sunset);
            var previous = Sun(date.AddDays(-1));
            var previousDaylight = DaylightMinutes(previous.Sunrise, previous.Sunset);
            var solsticeDaylight = references[SolsticeDaylight.LastSolstice(date)]?.DaylightMinutes;
            sky[date] = new AlmanacSky(Clock(sunrise), Clock(sunset), day?.Moon, daylight, daylight - previousDaylight, daylight - solsticeDaylight);
        }

        return sky;
    }

    // A window that reaches past today is asked for again tomorrow, with one more day. A complete one never changes.
    private Task<SolsticeReference?> GetSolsticeAsync(GeoPoint location, DateOnly solstice, DateOnly today)
    {
        var (start, end) = SolsticeDaylight.Window(solstice, today);
        var complete = end == solstice.AddDays(2);
        return GetOptionalAsync<SolsticeReference>(
            "Open-Meteo archive",
            string.Create(CultureInfo.InvariantCulture, $"solstice:{location.Latitude},{location.Longitude}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}"),
            complete ? SolsticeCacheDuration : MoonCacheDuration,
            token => solsticeDaylight.GetAsync(location, solstice, start, end, token));
    }

    // Open-Meteo's "2026-10-30T07:14" as "07:14", to match USNO.
    private static string? Clock(string? time) => time?[(time.IndexOf('T') + 1)..];

    /// <summary>
    /// Minutes from sunrise to sunset. Open-Meteo's "2026-06-21T04:57" times keep their dates, so a sunset after
    /// midnight still counts, and Open-Meteo's polar day (midnight to the next midnight) is 24 hours and its
    /// polar night (both at the same midnight) none. USNO's "HH:mm" times have no date, so with one of those the
    /// sunset must be later than the sunrise. Null when either is missing or they can't be compared.
    /// </summary>
    public static int? DaylightMinutes(string? sunrise, string? sunset)
    {
        if (LocalDateTime(sunrise) is { } riseAt && LocalDateTime(sunset) is { } setAt)
        {
            return setAt >= riseAt ? (int)(setAt - riseAt).TotalMinutes : null;
        }

        return ClockMinutes(Clock(sunrise)) is int rise && ClockMinutes(Clock(sunset)) is int set && set > rise ? set - rise : null;
    }

    private static DateTime? LocalDateTime(string? time) =>
        DateTime.TryParseExact(time, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : null;

    private static int? ClockMinutes(string? time) =>
        TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var clock)
            ? (clock.Hour * 60) + clock.Minute
            : null;

    /// <summary>Returns null when the ID is badly formed. A discussion that can't be loaded is left out.</summary>
    public async Task<DiscussionReport?> GetDiscussionAsync(string? stationId = null)
    {
        if (directory.Find(stationId) is not { } station)
        {
            return null;
        }

        if (!station.CanLocate)
        {
            return new DiscussionReport(null);
        }

        var office = await GetOptionalAsync(
            "NWS office",
            $"nws-office:{station.Id}",
            ForecastCacheDuration,
            async token => await discussion.GetOfficeAsync(await locator.GetAsync(station, token), token));
        if (office is null)
        {
            return new DiscussionReport(null);
        }

        // Cached per office, so stations that share one make its two calls once.
        return new DiscussionReport(await GetOptionalAsync(
            "NWS discussion",
            $"discussion:{office}",
            ForecastCacheDuration,
            token => discussion.GetAsync(office, token)));
    }

    private DateTimeOffset LocalNow(Outlook forecast) =>
        TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(forecast.TimeZoneId));

    private DateOnly Today(Outlook forecast) => DateOnly.FromDateTime(LocalNow(forecast).DateTime);

    // Cached forecasts can outlive midnight, so days that have passed are dropped here.
    private List<ForecastDay> RemainingDays(Outlook forecast)
    {
        var today = Today(forecast);
        return forecast.Days.Where(day => day.Date >= today).ToList();
    }

    // Today only lists the current hour onwards.
    private List<ForecastHour> RemainingHours(Outlook forecast, DateOnly date)
    {
        var currentHour = LocalNow(forecast).ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);

        return forecast.Hours[date]
            .Where(hour => string.CompareOrdinal(hour.Time, 0, currentHour, 0, currentHour.Length) >= 0)
            .ToList();
    }

    private static string PeriodStart(DateOnly date, int hour) =>
        string.Create(CultureInfo.InvariantCulture, $"{date:yyyy-MM-dd}T{hour:00}");

    // Hour times start with this text, so they sort correctly against a period's limits.
    private static PeriodPrecip? Precipitation(List<ForecastHour> hours, string start, string end)
    {
        var period = hours
            .Where(hour => string.CompareOrdinal(hour.Time, start) >= 0 && string.CompareOrdinal(hour.Time, end) < 0)
            .ToList();

        if (period.Count == 0)
        {
            return null;
        }

        return new PeriodPrecip(
            period.Max(hour => hour.PrecipChancePercent),
            Total(period, hour => hour.PrecipIn),
            Total(period, hour => hour.SnowIn));
    }

    private static double? Total(List<ForecastHour> hours, Func<ForecastHour, double?> amount)
    {
        var amounts = hours.Select(amount).OfType<double>().ToList();
        return amounts.Count == 0 ? null : Math.Round(amounts.Sum(), 2);
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
            "NWS descriptions",
            string.Create(CultureInfo.InvariantCulture, $"descriptions:{location.Latitude},{location.Longitude}"),
            ForecastCacheDuration,
            async token => await descriptions.GetAsync(location, token));

    // The almanac and the day page share this call.
    private Task<UsnoDay?> GetUsnoAsync(GeoPoint location, DateOnly date, string timeZoneId) =>
        GetOptionalAsync(
            "USNO",
            string.Create(CultureInfo.InvariantCulture, $"moon:{location.Latitude},{location.Longitude}:{date:yyyy-MM-dd}"),
            MoonCacheDuration,
            token => moon.GetAsync(location, date, timeZoneId, token));

    // These sources only add detail, so a failure hides their section and nothing else.
    private Task<T?> GetOptionalAsync<T>(string source, string key, TimeSpan ttl, Func<CancellationToken, Task<T>> fetch) where T : class =>
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
                    logger.LogWarning("{Source} failed: {Error}", source, exception.GetType().Name);
                    return (null, ErrorRetryDuration);
                }
            });

    private sealed record Fetched(Outlook? Forecast, GeoPoint? Location, string? Message);
}
