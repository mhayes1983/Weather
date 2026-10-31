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

        var averageHigh = rows[0].AverageF is int start && rows[^1].AverageF is int end
            ? new AverageHigh(start, end, normals!.Station)
            : null;

        return new ForecastTable(
            location,
            now,
            averageHigh,
            averageHigh is null ? "Average highs are unavailable right now." : null,
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
        int? average = normals is not null && normals.HighsF.TryGetValue(date, out var normal) ? normal : null;

        var cells = new Dictionary<string, ForecastCell>();
        for (var i = 0; i < applicable.Count; i++)
        {
            if (results[i].Highs.FirstOrDefault(high => high.Date == date) is { } high)
            {
                cells[applicable[i].Column.Id] = new ForecastCell(
                    high.HighF,
                    AnomalyClassifier.Classify(high.HighF, average, options.Value.AboveAverageF, options.Value.BelowAverageF));
            }
        }

        return new ForecastRow(date, average, cells);
    }

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
            return await source.GetDailyHighsAsync(location, timeout.Token);
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
            logger.LogWarning("Average highs failed for {Zip}: {Error}", location.Zip, exception.GetType().Name);
            return null;
        }
    }
}
