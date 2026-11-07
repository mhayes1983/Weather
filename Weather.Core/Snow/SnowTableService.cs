using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Snow;

public sealed record SnowRequest(IReadOnlyList<string> Zips, DateOnly Start, DateOnly End);

/// <summary>
/// Builds the Snow Forecast table: each source's snow total for each town over a date range.
/// One bad source or town only blanks its own cells.
/// </summary>
public sealed class SnowTableService(
    IEnumerable<ISnowSource> sources,
    LocationResolver locationResolver,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<SnowTableService> logger)
{
    public const int MaxTowns = 15;
    public const int MaxDays = 7;
    public const int MaxDaysAhead = 15;
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);

    public IReadOnlyList<SnowTownSummary> Towns => options.Value.SnowTowns
        .Where(town => LocationResolver.IsValidZip(town.Zip))
        .DistinctBy(town => town.Zip)
        .Select(town => new SnowTownSummary(town.Zip, string.IsNullOrWhiteSpace(town.Name) ? town.Zip : town.Name))
        .ToList();

    /// <summary>Today at the default location, which bounds the dates a request may ask for.</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
        timeProvider.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(options.Value.DefaultLocation.TimeZoneId)).DateTime);

    /// <summary>
    /// Reads the query. Blank ZIPs mean the configured towns, a blank start today, and a blank end
    /// the day after the start. Returns an error message for anything else that's invalid.
    /// </summary>
    public (SnowRequest? Request, string? Error) Parse(string? zips, string? start, string? end)
    {
        var list = string.IsNullOrWhiteSpace(zips)
            ? Towns.Select(town => town.Zip).ToList()
            : zips.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

        if (list.Count == 0 || !list.All(LocationResolver.IsValidZip))
        {
            return (null, "Enter 5-digit ZIP codes.");
        }

        if (list.Count > MaxTowns)
        {
            return (null, $"Choose at most {MaxTowns} towns.");
        }

        var today = Today;
        if (!TryDate(start, today, out var from) || !TryDate(end, from.AddDays(1), out var to))
        {
            return (null, "Enter dates as YYYY-MM-DD.");
        }

        if (to < from)
        {
            return (null, "The end date can't be before the start date.");
        }

        if (from < today.AddDays(-1) || to > today.AddDays(MaxDaysAhead))
        {
            return (null, $"Choose dates from yesterday to {MaxDaysAhead} days ahead.");
        }

        if (to.DayNumber - from.DayNumber + 1 > MaxDays)
        {
            return (null, $"Choose a range of {MaxDays} days or fewer.");
        }

        return (new SnowRequest(list, from, to), null);
    }

    public async Task<SnowTable> GetAsync(SnowRequest request)
    {
        var configured = Towns.ToDictionary(town => town.Zip, town => town.Name);
        var located = await Task.WhenAll(request.Zips.Select(zip => LocateAsync(zip, configured)));

        var cells = new List<SnowCell>();
        var rows = new List<SnowSourceRow>();
        foreach (var source in sources)
        {
            var towns = located.Where(town => town.Location is not null && source.AppliesTo(town.Location)).ToList();
            if (towns.Count == 0)
            {
                continue;
            }

            var results = await Task.WhenAll(towns.Select(town => GetSourceResultAsync(source, town.Location!)));
            for (var i = 0; i < towns.Count; i++)
            {
                cells.Add(Cell(source, towns[i].Location!, results[i], request));
            }

            rows.Add(Row(source, results));
        }

        return new SnowTable(
            request.Start,
            request.End,
            timeProvider.GetUtcNow(),
            located.Select(town => town.Town).ToList(),
            rows,
            cells);
    }

    private static SnowCell Cell(ISnowSource source, ForecastLocation location, SnowResult result, SnowRequest request)
    {
        if (result.Status != SourceStatus.Ok)
        {
            return new SnowCell(source.Column.Id, location.Zip, null, null, false, result.Status, result.Message);
        }

        var (start, end) = SnowWindow.Bounds(request.Start, request.End, location.TimeZoneId);
        var total = SnowWindow.Total(result.Periods, start, end);
        return new SnowCell(
            source.Column.Id,
            location.Zip,
            total?.Low,
            total?.High,
            total?.Partial ?? false,
            SourceStatus.Ok,
            total is null ? "No forecast for these dates" : result.Message);
    }

    // A row only shows Needs key or an error when every town has it; otherwise the cells say which failed.
    private static SnowSourceRow Row(ISnowSource source, IReadOnlyList<SnowResult> results)
    {
        var status = results.All(result => result.Status == SourceStatus.NeedsKey) ? SourceStatus.NeedsKey
            : results.All(result => result.Status == SourceStatus.Error) ? SourceStatus.Error
            : SourceStatus.Ok;
        var message = results.Select(result => result.Message).FirstOrDefault(text => text is not null);
        var fetchedAt = results.Where(result => result.FetchedAt is not null).Select(result => result.FetchedAt).Min();
        return new SnowSourceRow(source.Column.Id, source.Column.Label, status, message, fetchedAt);
    }

    private async Task<(SnowTown Town, ForecastLocation? Location)> LocateAsync(
        string zip, IReadOnlyDictionary<string, string> configured)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            var location = await locationResolver.ResolveAsync(zip, timeout.Token);
            var name = configured.TryGetValue(zip, out var configuredName) ? configuredName : $"{location.Place}, {location.State}";
            return (new SnowTown(zip, name, null), location);
        }
        catch (LocationNotFoundException)
        {
            return (new SnowTown(zip, zip, "That ZIP code wasn't found."), null);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Snow town {Zip} failed: {Error}", zip, exception.GetType().Name);
            return (new SnowTown(zip, configured.GetValueOrDefault(zip, zip), "The ZIP code lookup is unavailable right now."), null);
        }
    }

    // Mirrors ForecastTableService: a result is cached for the source, and a failure falls back to the last good one.
    private Task<SnowResult> GetSourceResultAsync(ISnowSource source, ForecastLocation location) =>
        cache.GetOrCreateAsync(
            $"snow:{source.Column.Id}:{location.Zip}",
            async () =>
            {
                var result = await FetchAsync(source, location);
                var lastGoodKey = $"snow-last-good:{source.Column.Id}:{location.Zip}";

                if (result.Status == SourceStatus.Ok)
                {
                    cache.Set(lastGoodKey, result, LastGoodDuration);
                    return (result, source.CacheDuration);
                }

                if (result.Status == SourceStatus.Error && cache.TryGet<SnowResult>(lastGoodKey, out var lastGood))
                {
                    result = lastGood! with { Message = $"Showing last good data. {result.Message}" };
                }

                return (result, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
            });

    // Each source has its own timeout, because its result is shared with other requests through the cache.
    private async Task<SnowResult> FetchAsync(ISnowSource source, ForecastLocation location)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            return await source.GetSnowAsync(location, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Snow {Column} failed for {Zip}: {Error}", source.Column.Id, location.Zip, exception.GetType().Name);
            return SnowResult.Error(SourceFailure.Describe(exception));
        }
    }

    private static bool TryDate(string? text, DateOnly fallback, out DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            date = fallback;
            return true;
        }

        return DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
