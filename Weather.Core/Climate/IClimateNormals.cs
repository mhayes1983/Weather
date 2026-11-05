using Weather.Core.Models;

namespace Weather.Core.Climate;

public sealed record ClimateNormals(
    string Station,
    IReadOnlyDictionary<DateOnly, int> HighsF,
    IReadOnlyDictionary<DateOnly, int> LowsF);

public interface IClimateNormals
{
    Task<ClimateNormals> GetAsync(ForecastLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken);
}
