using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>A snowfall forecast for the Snow Forecast page. Throws on failure, like <c>IForecastSource</c>.</summary>
public interface ISnowSource
{
    ColumnDescriptor Column { get; }

    TimeSpan CacheDuration { get; }

    /// <summary>
    /// The <see cref="StormViews"/> this source can give. While it works, the data it sends decides, because some
    /// Open-Meteo models return no gusts or depth; when it fails, its error row still shows in these views, so an
    /// outage never looks like a source without that data.
    /// </summary>
    IReadOnlyList<string> Views { get; }

    bool AppliesTo(ForecastLocation location);

    /// <summary>Snow in periods, so any date range can be totalled from one cached result.</summary>
    Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken);
}
