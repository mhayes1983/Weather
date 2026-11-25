namespace Weather.Core;

public static class GeoDistance
{
    private const double EarthRadiusMiles = 3958.8;

    /// <summary>The great-circle distance between two points, in miles.</summary>
    public static double Miles(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var latitude = Radians(latitude2 - latitude1);
        var longitude = Radians(longitude2 - longitude1);
        var a = (Math.Sin(latitude / 2) * Math.Sin(latitude / 2))
            + (Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2)) * Math.Sin(longitude / 2) * Math.Sin(longitude / 2));
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
