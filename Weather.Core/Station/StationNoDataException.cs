namespace Weather.Core.Station;

public sealed class StationNoDataException() : Exception("The station has no recent data.");
