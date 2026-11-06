using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Climate;

/// <summary>A climate station's averages, records and measurements for yesterday, today and tomorrow, in that order.</summary>
public sealed record ClimateAlmanac(string Station, int? RecordsSince, IReadOnlyList<AlmanacDay> Days);

/// <summary>
/// Almanac data for yesterday, today and tomorrow from the location's climate station, chosen by
/// <see cref="ClimateStations"/>, so its averages match the forecast table's.
/// </summary>
public sealed class AcisAlmanac(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    ClimateStations stations,
    IOptions<WeatherOptions> options)
{
    private static readonly TimeSpan RecordsCacheDuration = TimeSpan.FromHours(24);

    // The record for each element is its highest value, except the low, which is its lowest.
    private static readonly (string Element, string Reduce)[] Elements =
        [("maxt", "max"), ("mint", "min"), ("pcpn", "max"), ("snow", "max")];

    private TimeSpan Timeout => TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds);

    /// <summary>Each ACIS call has its own timeout, so a slow one can't use up another's time.</summary>
    /// <exception cref="NoClimateStationException">No suitable station is nearby.</exception>
    public async Task<ClimateAlmanac> GetAsync(GeoPoint location, DateOnly today)
    {
        var station = await stations.FindAsync(location.Latitude, location.Longitude, today);

        // Weather stations near each other share this entry, so it keeps its own timeout rather than the first caller's.
        var records = await cache.GetOrCreateAsync(
            $"almanac-records:{station.Id}:{today:yyyy-MM-dd}",
            async () => (await Acis.WithTimeoutAsync(Timeout, token => GetRecordsAsync(station, today, token)), RecordsCacheDuration));

        var daily = await Acis.WithTimeoutAsync(Timeout, token => GetDailyAsync(station, today, token));

        return new ClimateAlmanac(
            station.Name,
            records.Since,
            Dates(today)
                .Select((date, index) => Day(date, daily.GetValueOrDefault(date), records.Days.ElementAtOrDefault(index) ?? []))
                .ToList());
    }

    private static DateOnly[] Dates(DateOnly today) => [today.AddDays(-1), today, today.AddDays(1)];

    // ACIS's summaries skip accumulated multi-day totals, so these are true one-day records.
    private async Task<Records> GetRecordsAsync(AcisStation station, DateOnly today, CancellationToken cancellationToken)
    {
        var elements = new JsonArray();
        foreach (var date in Dates(today))
        {
            var monthDay = date.ToString("MM-dd", CultureInfo.InvariantCulture);
            foreach (var (element, reduce) in Elements)
            {
                elements.Add(new JsonObject
                {
                    ["name"] = element,
                    ["interval"] = "dly",
                    ["duration"] = "dly",
                    ["smry"] = new JsonObject { ["reduce"] = reduce, ["add"] = "date" },
                    ["smry_only"] = 1,
                    ["groupby"] = new JsonArray("year", monthDay, monthDay),
                });
            }
        }

        var body = new JsonObject
        {
            ["sid"] = station.Id,
            ["sdate"] = "por",
            ["edate"] = Acis.Date(today.AddDays(-1)),
            ["elems"] = elements,
            ["meta"] = "valid_daterange",
        };

        using var document = await Acis.PostAsync(httpClientFactory, "StnData", body, cancellationToken);
        var summaries = document.RootElement.GetProperty("smry");
        var records = Enumerable.Range(0, summaries.GetArrayLength())
            .Select(index => Record(summaries[index], IsAmount(index % Elements.Length)))
            .ToList();

        int? since = document.RootElement.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("valid_daterange", out var ranges)
            && ranges.ValueKind == JsonValueKind.Array && ranges.GetArrayLength() > 0
            && ranges[0].ValueKind == JsonValueKind.Array && ranges[0].GetArrayLength() == 2
            && Acis.ParseDate(ranges[0][0]) is { } start
                ? start.Year
                : null;

        // Summaries come in the order requested: each date's elements together.
        return new Records(since, records.Chunk(Elements.Length).Select(day => (IReadOnlyList<(double?, int?)>)day).ToList());
    }

    private static bool IsAmount(int element) => Elements[element].Element is "pcpn" or "snow";

    // Each summary is a list of groups, and grouping by one day of the year makes one group.
    // A record of no rain or snow means none has fallen on that date, so it has no year.
    private static (double? Value, int? Year) Record(JsonElement summary, bool isAmount)
    {
        if (summary.ValueKind != JsonValueKind.Array || summary.GetArrayLength() == 0
            || summary[0] is not { ValueKind: JsonValueKind.Array } group || group.GetArrayLength() < 2)
        {
            return (null, null);
        }

        var value = Number(group[0]);
        return value is null ? (null, null)
            : isAmount && value == 0 ? (value, null)
            : (value, Acis.ParseDate(group[1])?.Year);
    }

    private async Task<Dictionary<DateOnly, double?[]>> GetDailyAsync(
        AcisStation station, DateOnly today, CancellationToken cancellationToken)
    {
        var elements = new JsonArray();
        foreach (var (element, _) in Elements)
        {
            elements.Add(new JsonObject { ["name"] = element });
        }

        foreach (var (element, _) in Elements)
        {
            elements.Add(Acis.NormalElement(element));
        }

        var body = new JsonObject
        {
            ["sid"] = station.Id,
            ["sdate"] = Acis.Date(today.AddDays(-1)),
            ["edate"] = Acis.Date(today.AddDays(1)),
            ["elems"] = elements,
        };

        using var document = await Acis.PostAsync(httpClientFactory, "StnData", body, cancellationToken);
        var days = new Dictionary<DateOnly, double?[]>();
        foreach (var day in document.RootElement.GetProperty("data").EnumerateArray())
        {
            if (Acis.ParseDate(day[0]) is { } date)
            {
                days[date] = Enumerable.Range(1, day.GetArrayLength() - 1).Select(index => Number(day[index])).ToArray();
            }
        }

        return days;
    }

    // Daily values are the measurements, then the normals, in the order of Elements.
    private static AlmanacDay Day(DateOnly date, double?[]? daily, IReadOnlyList<(double? Value, int? Year)> records)
    {
        AlmanacRow Row(int index)
        {
            var record = index < records.Count ? records[index] : (null, null);
            return new AlmanacRow(
                Value(daily, index),
                Value(daily, index + Elements.Length),
                record.Value,
                record.Year);
        }

        return new AlmanacDay(date, Row(0), Row(1), Row(2), Row(3));
    }

    private static double? Value(double?[]? values, int index) =>
        values is not null && index < values.Length ? values[index] : null;

    /// <summary>
    /// ACIS sends values as text. "T" is a trace. "M" is missing, and "S" means the amount is included in a
    /// later total. A value with a letter after it, such as "3.39A", is that accumulated multi-day total, so it
    /// isn't one day's amount and counts as missing too.
    /// </summary>
    internal static double? Number(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()!;
        if (text == "T")
        {
            return AlmanacRow.TraceInches;
        }

        return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private sealed record Records(
        int? Since,
        IReadOnlyList<IReadOnlyList<(double? Value, int? Year)>> Days);
}
