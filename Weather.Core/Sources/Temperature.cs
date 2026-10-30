namespace Weather.Core.Sources;

internal static class Temperature
{
    public static int ToWholeDegrees(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
