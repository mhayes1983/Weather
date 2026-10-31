using Weather.Core.Models;

namespace Weather.Core.Station;

public interface IWeatherStation
{
    string Id { get; }

    string Name { get; }

    string Source { get; }

    StationHistoryRange HistoryRange { get; }

    TimeSpan CurrentCacheDuration { get; }

    TimeSpan HistoryCacheDuration { get; }

    bool HasKeys { get; }

    Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken);
}
