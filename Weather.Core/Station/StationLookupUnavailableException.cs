namespace Weather.Core.Station;

public sealed class StationLookupUnavailableException(string message) : Exception(message);
