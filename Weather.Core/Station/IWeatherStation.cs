using Weather.Core.Models;

namespace Weather.Core.Station;

public interface IWeatherStation
{
    bool HasKeys { get; }

    Task<StationReading> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StationHistoryPoint>> GetHistoryAsync(CancellationToken cancellationToken);
}
