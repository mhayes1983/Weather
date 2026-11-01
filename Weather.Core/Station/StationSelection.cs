namespace Weather.Core.Station;

public static class StationSelection
{
    /// <summary>
    /// Separates chosen IDs into default stations and added ones. Station IDs aren't case
    /// sensitive here, so a typed default takes its configured spelling and isn't added twice.
    /// </summary>
    public static (List<string> Defaults, List<string> Added) Split(IEnumerable<string> ids, IEnumerable<string> defaults)
    {
        var configured = defaults.ToDictionary(id => id, StringComparer.OrdinalIgnoreCase);
        var chosen = ids
            .Where(StationDirectory.IsValidId)
            .Select(id => configured.GetValueOrDefault(id, id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ([.. chosen.Where(configured.ContainsKey)], [.. chosen.Where(id => !configured.ContainsKey(id))]);
    }
}
