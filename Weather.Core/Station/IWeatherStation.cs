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

    TimeSpan OfflineAfter { get; }

    bool HasKeys { get; }

    bool CanLocate { get; }

    Task<GeoPoint> GetCoordinatesAsync(CancellationToken cancellationToken);

    Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken);
}
