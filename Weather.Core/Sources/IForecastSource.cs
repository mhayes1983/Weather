using Weather.Core.Models;

namespace Weather.Core.Sources;

public interface IForecastSource
{
    ColumnDescriptor Column { get; }

    TimeSpan CacheDuration { get; }

    bool AppliesTo(ForecastLocation location);

    Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken);
}
