namespace Weather.Core.Models;

public sealed record ForecastLocation
{
    public string Zip { get; init; } = "";
    public string Place { get; init; } = "";
    public string State { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string TimeZoneId { get; init; } = "";
}
