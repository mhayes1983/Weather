using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>
/// Snow and rain totals and wind gusts reported to the NWS near each town (Local Storm Reports), from the Iowa
/// Environmental Mesonet's archive, all from one call per state. They're observed, so they sit beside the
/// forecasts and never join the averages. A failure only says the reports are unavailable.
/// </summary>
public sealed class SnowReportService(
    IHttpClientFactory httpClientFactory,
    LocationResolver locationResolver,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<SnowReportService> logger)
{
    public const double RadiusMiles = 10;
    public const int MaxReportsPerTown = 8;

    private const string Endpoint = "https://mesonet.agron.iastate.edu/geojson/lsr.geojson";
    // Report types: S is snow and R rain, in inches. N is a non-thunderstorm gust and G a thunderstorm gust, in mph.
    private const string SnowType = "S";
    private const string RainType = "R";
    private static readonly string[] GustTypes = ["N", "G"];
    private const double EarthRadiusMiles = 3958.8;

    // Storm totals are often sent in the morning after the snow ends.
    private static readonly TimeSpan AfterWindow = TimeSpan.FromHours(12);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<SnowReportTable> GetAsync(SnowRequest request)
    {
        var now = timeProvider.GetUtcNow();
        var located = (await Task.WhenAll(request.Zips.Select(LocateAsync))).OfType<ForecastLocation>().ToList();

        var towns = new List<SnowTownReports>();
        var rainTowns = new List<SnowTownReports>();
        var windTowns = new List<SnowTownReports>();
        DateOnly? rainSince = null;
        var failed = false;
        foreach (var state in located.GroupBy(location => location.State.ToUpperInvariant()))
        {
            var windows = state
                .Select(location =>
                {
                    var (start, end) = SnowWindow.Bounds(request.Start, request.End, location.TimeZoneId);

                    // Rain is what has already fallen, from midnight 7 days ago to the end of today, so the page has
                    // reports whatever the storm's dates. Its end is fixed for the day so the cached call is reused.
                    var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId);
                    var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
                    var (rainStart, rainEnd) = SnowWindow.Bounds(today.AddDays(-RainDays), today, location.TimeZoneId);
                    return (Location: location, Start: start, End: end + AfterWindow, Started: start < now,
                        RainStart: rainStart, RainEnd: rainEnd, RainSince: today.AddDays(-RainDays));
                })
                .ToList();

            if (state.Key.Length != 2 || !state.Key.All(char.IsAsciiLetter))
            {
                failed = true;
                continue;
            }

            // Snow and gusts only count once their dates have started. One call covers them and the past week's rain.
            var started = windows.Where(window => window.Started).ToList();
            var from = windows.Min(window => window.RainStart);
            var until = started.Select(window => window.End).Append(windows.Max(window => window.RainEnd)).Max();
            var reports = await GetStateAsync(state.Key, from, until);
            if (reports is null)
            {
                failed = true;
                continue;
            }

            foreach (var window in windows)
            {
                rainSince = window.RainSince;
                rainTowns.Add(ForTown(window.Location, window.RainStart, window.RainEnd, reports, ReportKind.Rain));
                if (window.Started)
                {
                    towns.Add(ForTown(window.Location, window.Start, window.End, reports, ReportKind.Snow));
                    windTowns.Add(ForTown(window.Location, window.Start, window.End, reports, ReportKind.Wind));
                }
            }
        }

        return new SnowReportTable(
            failed ? SourceStatus.Error : SourceStatus.Ok,
            failed ? "Storm reports are unavailable for some towns right now." : null,
            RadiusMiles,
            towns,
            rainTowns,
            windTowns,
            rainSince);
    }

    /// <summary>Rain reports cover this many days before today, as well as today.</summary>
    public const int RainDays = 7;

    /// <summary>Rain covers a week of separate events, so it lists more of them.</summary>
    public const int MaxRainReportsPerTown = 12;

    // A place can report several times as a storm goes on. Its highest is its total so far, or its strongest gust.
    // Rain covers a week of separate events, so a place keeps its highest report on each date instead, newest first.
    private static SnowTownReports ForTown(
        ForecastLocation location, DateTimeOffset start, DateTimeOffset end, IReadOnlyList<Report> reports, ReportKind kind)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId);
        var byDate = kind == ReportKind.Rain;
        var grouped = reports
            .Where(report => report.Kind == kind && report.ObservedAt >= start && report.ObservedAt <= end)
            .Select(report => (Report: report, Miles: Miles(location.Latitude, location.Longitude, report.Latitude, report.Longitude)))
            .Where(item => item.Miles <= RadiusMiles)
            .GroupBy(item => (
                item.Report.Place,
                item.Report.Latitude,
                item.Report.Longitude,
                Date: byDate ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.Report.ObservedAt, zone).DateTime) : default))
            .Select(place => place.OrderByDescending(item => item.Report.Amount).ThenByDescending(item => item.Report.ObservedAt).First());
        var nearby = (byDate
                ? grouped.OrderByDescending(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.Report.ObservedAt, zone).DateTime)).ThenBy(item => item.Miles)
                : grouped.OrderBy(item => item.Miles))
            .ToList();

        return new SnowTownReports(
            location.Zip,
            nearby.Count == 0 ? null : nearby.Min(item => item.Report.Amount),
            nearby.Count == 0 ? null : nearby.Max(item => item.Report.Amount),
            nearby
                .Take(byDate ? MaxRainReportsPerTown : MaxReportsPerTown)
                .Select(item => new SnowReport(
                    item.Report.Place,
                    item.Report.Amount,
                    TimeZoneInfo.ConvertTime(item.Report.ObservedAt, zone),
                    Math.Round(item.Miles, 1),
                    item.Report.Source,
                    item.Report.Estimated))
                .ToList());
    }

    // The result is shared through the cache, so it has its own timeout. Null means the reports couldn't be loaded.
    private Task<IReadOnlyList<Report>?> GetStateAsync(string state, DateTimeOffset start, DateTimeOffset end) =>
        cache.GetOrCreateAsync<IReadOnlyList<Report>?>(
            string.Create(CultureInfo.InvariantCulture, $"snow-reports:{state}:{start.UtcDateTime:yyyyMMddHHmm}:{end.UtcDateTime:yyyyMMddHHmm}"),
            async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
                try
                {
                    var url = string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Endpoint}?sts={start.UtcDateTime:yyyyMMddHHmm}&ets={end.UtcDateTime:yyyyMMddHHmm}&states={state}");

                    var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
                    return (Read(await client.GetStringAsync(url, timeout.Token)), CacheDuration);
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Snow reports for {State} failed: {Error}", state, exception.GetType().Name);
                    return (null, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
                }
            });

    /// <summary>
    /// Snow and rain reports in inches and gusts in mph. Other report types, and ones without an amount or a place, are left out.
    /// </summary>
    public static IReadOnlyList<Report> Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        var reports = new List<Report>();
        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var properties = feature.GetProperty("properties");
            if (Kind(Text(properties, "type"), Text(properties, "unit")) is not { } kind
                || Number(properties, "magf") is not { } amount
                || Number(properties, "lat") is not { } latitude
                || Number(properties, "lon") is not { } longitude
                || Text(properties, "city") is not { Length: > 0 } place
                || !DateTimeOffset.TryParse(Text(properties, "valid"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var observedAt))
            {
                continue;
            }

            reports.Add(new Report(kind, place, amount, observedAt, latitude, longitude, Text(properties, "source"), Text(properties, "qualifier") == "E"));
        }

        return reports;
    }

    private static ReportKind? Kind(string? type, string? unit) =>
        type == SnowType && string.Equals(unit, "Inch", StringComparison.OrdinalIgnoreCase) ? ReportKind.Snow
        : type == RainType && string.Equals(unit, "Inch", StringComparison.OrdinalIgnoreCase) ? ReportKind.Rain
        : GustTypes.Contains(type) && string.Equals(unit, "MPH", StringComparison.OrdinalIgnoreCase) ? ReportKind.Wind
        : null;

    private async Task<ForecastLocation?> LocateAsync(string zip)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            return await locationResolver.ResolveAsync(zip, timeout.Token);
        }
        catch (Exception exception)
        {
            // The table already says why a town couldn't be found.
            logger.LogWarning("Snow reports town {Zip} failed: {Error}", zip, exception.GetType().Name);
            return null;
        }
    }

    private static double Miles(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var latitude = Radians(latitude2 - latitude1);
        var longitude = Radians(longitude2 - longitude1);
        var a = (Math.Sin(latitude / 2) * Math.Sin(latitude / 2))
            + (Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2)) * Math.Sin(longitude / 2) * Math.Sin(longitude / 2));
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static string? Text(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    public enum ReportKind
    {
        Snow,
        Rain,
        Wind,
    }

    /// <summary>
    /// One report as the archive sends it, in inches or, for wind, mph. The coordinates are only used to measure distance.
    /// </summary>
    public sealed record Report(
        ReportKind Kind,
        string Place,
        double Amount,
        DateTimeOffset ObservedAt,
        double Latitude,
        double Longitude,
        string? Source,
        bool Estimated);
}
