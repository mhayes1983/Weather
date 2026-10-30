namespace Weather.Core.Locations;

public sealed class LocationNotFoundException(string zip)
    : Exception($"ZIP code {zip} was not found.");
