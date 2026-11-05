using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Climate;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core;

public sealed class ForecastTableService(
    IEnumerable<IForecastSource> sources,
    IClimateNormals climateNormals,
    LocationResolver locationResolver,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    ILogger<ForecastTableService> logger)
{
    private static readonly TimeSpan LastGoodDuration = TimeSpan.FromHours(24);

    public string DefaultZip => options.Value.DefaultLocation.Zip;

    public async Task<ForecastTable> GetAsync(string zip, CancellationToken cancellationToken = default)
    {
        var location = await locationResolver.ResolveAsync(zip, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            now, TimeZoneInfo.FindSystemTimeZoneById(location.TimeZoneId)).DateTime);
        var dates = Enumerable.Range(0, options.Value.ForecastDays).Select(today.AddDays).ToList();

        var applicable = sources.Where(source => source.AppliesTo(location)).ToList();
        var normalsTask = GetNormalsAsync(location, dates[0], dates[^1]);
        var results = await Task.WhenAll(applicable.Select(source => GetSourceResultAsync(source, location)));
        var normals = await normalsTask;

        var rows = dates.Select(date => BuildRow(date, normals, applicable, results)).ToList();
        var columns = applicable.Zip(results, (source, result) => new ForecastColumn(
            source.Column.Id, source.Column.Label, result.Status, result.Message, result.FetchedAt)).ToList();

        var averageHigh = rows[0].AverageHighF is int highStart && rows[^1].AverageHighF is int highEnd
            ? new AverageRange(highStart, highEnd, normals!.Station)
            : null;
        var averageLow = rows[0].AverageLowF is int lowStart && rows[^1].AverageLowF is int lowEnd
            ? new AverageRange(lowStart, lowEnd, normals!.Station)
            : null;

        return new ForecastTable(
            location,
            now,
            averageHigh,
            averageLow,
            averageHigh is null && averageLow is null ? "Averages are unavailable right now." : null,
            options.Value.AboveAverageF,
            options.Value.BelowAverageF,
            columns,
            rows);
    }

    private ForecastRow BuildRow(
        DateOnly date,
        ClimateNormals? normals,
        IReadOnlyList<IForecastSource> applicable,
        IReadOnlyList<SourceResult> results)
    {
        int? averageHigh = normals is not null && normals.HighsF.TryGetValue(date, out var normalHigh) ? normalHigh : null;
        int? averageLow = normals is not null && normals.LowsF.TryGetValue(date, out var normalLow) ? normalLow : null;

        var cells = new Dictionary<string, ForecastCell>();
        for (var i = 0; i < applicable.Count; i++)
        {
            if (results[i].Days.FirstOrDefault(day => day.Date == date) is { } day)
            {
                cells[applicable[i].Column.Id] = new ForecastCell(
                    day.HighF,
                    Classify(day.HighF, averageHigh),
                    day.LowF,
                    Classify(day.LowF, averageLow));
            }
        }

        return new ForecastRow(date, averageHigh, averageLow, cells);
    }

    // Lows use the same thresholds as highs, against the normal low.
    private Anomaly Classify(int? valueF, int? averageF) => valueF is int value
        ? AnomalyClassifier.Classify(value, averageF, options.Value.AboveAverageF, options.Value.BelowAverageF)
        : Anomaly.None;

    private Task<SourceResult> GetSourceResultAsync(IForecastSource source, ForecastLocation location)
    {
        return cache.GetOrCreateAsync(
            $"source:{source.Column.Id}:{location.Zip}",
            async () =>
            {
                var result = await FetchAsync(source, location);
                var lastGoodKey = $"last-good:{source.Column.Id}:{location.Zip}";

                if (result.Status == SourceStatus.Ok)
                {
                    cache.Set(lastGoodKey, result, LastGoodDuration);
                    return (result, source.CacheDuration);
                }

                if (result.Status == SourceStatus.Error && cache.TryGet<SourceResult>(lastGoodKey, out var lastGood))
                {
                    result = lastGood! with { Message = $"Showing last good data. {result.Message}" };
                }

                return (result, TimeSpan.FromMinutes(options.Value.ErrorRetryMinutes));
            });
    }

    // Each source has its own timeout rather than the triggering request's token,
    // because the result is shared with other requests through the cache.
    private async Task<SourceResult> FetchAsync(IForecastSource source, ForecastLocation location)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            return await source.GetDailyTemperaturesAsync(location, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("{Column} failed for {Zip}: {Error}", source.Column.Id, location.Zip, exception.GetType().Name);
            return SourceResult.Error(SourceFailure.Describe(exception));
        }
    }

    private async Task<ClimateNormals?> GetNormalsAsync(ForecastLocation location, DateOnly start, DateOnly end)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            return await climateNormals.GetAsync(location, start, end, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Averages failed for {Zip}: {Error}", location.Zip, exception.GetType().Name);
            return null;
        }
    }
}
