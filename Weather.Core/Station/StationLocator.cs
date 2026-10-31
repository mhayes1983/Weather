using Weather.Core.Models;

namespace Weather.Core.Station;

/// <summary>Finds a station's coordinates once a month, so forecasts and alerts share the lookup.</summary>
public sealed class StationLocator(WeatherCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);

    public Task<GeoPoint> GetAsync(IWeatherStation station, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"station-location:{station.Id}",
            async () => (await station.GetCoordinatesAsync(cancellationToken), CacheDuration));
}
