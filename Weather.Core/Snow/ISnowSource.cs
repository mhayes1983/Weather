using Weather.Core.Models;

namespace Weather.Core.Snow;

/// <summary>A snowfall forecast for the Snow Forecast page. Throws on failure, like <c>IForecastSource</c>.</summary>
public interface ISnowSource
{
    ColumnDescriptor Column { get; }

    TimeSpan CacheDuration { get; }

    bool AppliesTo(ForecastLocation location);

    /// <summary>Snow in periods, so any date range can be totalled from one cached result.</summary>
    Task<SnowResult> GetSnowAsync(ForecastLocation location, CancellationToken cancellationToken);
}
