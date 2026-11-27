namespace Weather.Core.Station;

/// <summary>
/// An Ambient Weather account couldn't be read and has no earlier list, so the station list would leave out
/// stations that still exist. Clients keep their saved stations rather than treating them as gone.
/// </summary>
public sealed class StationListUnavailableException(string message) : Exception(message);
