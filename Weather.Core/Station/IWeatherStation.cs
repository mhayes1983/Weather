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

    /// <summary>True when the values come from a weather model rather than instruments.</summary>
    bool Modelled => false;

    /// <summary>A longer gap between history points shows as a break in the charts.</summary>
    int HistoryMaxGapMinutes => StationReport.DefaultHistoryMaxGapMinutes;

    Task<GeoPoint> GetCoordinatesAsync(CancellationToken cancellationToken);

    Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken);
}
