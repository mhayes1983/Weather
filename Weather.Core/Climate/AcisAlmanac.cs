using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Weather.Core.Models;

namespace Weather.Core.Climate;

/// <summary>A climate station's averages, records and measurements for yesterday, today and tomorrow, in that order.</summary>
public sealed record ClimateAlmanac(string Station, int? RecordsSince, IReadOnlyList<AlmanacDay> Days);

/// <summary>
/// Almanac data for yesterday, today and tomorrow from the nearest ACIS station that still reports, has kept records for
/// <see cref="MinimumRecordYears"/> years and has normals. That can differ from the
/// normals station, which only needs normals and may have closed.
/// </summary>
public sealed class AcisAlmanac(IHttpClientFactory httpClientFactory, WeatherCache cache)
{
    public const int MinimumRecordYears = 30;

    // Co-op observers can report a few days late, so a station this recent still counts as active.
    private const int ActiveWithinDays = 7;

    private static readonly TimeSpan StationCacheDuration = TimeSpan.FromDays(30);
    private static readonly TimeSpan RecordsCacheDuration = TimeSpan.FromHours(24);

    // The record for each element is its highest value, except the low, which is its lowest.
    private static readonly (string Element, string Reduce)[] Elements =
        [("maxt", "max"), ("mint", "min"), ("pcpn", "max"), ("snow", "max")];

    /// <param name="cacheKey">Identifies the weather station, so its climate station is found once a month.</param>
    public async Task<ClimateAlmanac> GetAsync(
        string cacheKey, GeoPoint location, DateOnly today, CancellationToken cancellationToken)
    {
        var station = await cache.GetOrCreateAsync(
            $"almanac-station:{cacheKey}",
            async () => (await FindStationAsync(location, today, cancellationToken), StationCacheDuration));

        var records = await cache.GetOrCreateAsync(
            $"almanac-records:{station.Id}:{today:yyyy-MM-dd}",
            async () => (await GetRecordsAsync(station, today, cancellationToken), RecordsCacheDuration));

        var daily = await GetDailyAsync(station, today, cancellationToken);

        return new ClimateAlmanac(
            station.Name,
            records.Since,
            Dates(today).Select((date, index) => Day(date, daily.GetValueOrDefault(date), records.Days.ElementAtOrDefault(index) ?? [])).ToList());
    }

    private static DateOnly[] Dates(DateOnly today) => [today.AddDays(-1), today, today.AddDays(1)];

    private Task<AcisStation> FindStationAsync(GeoPoint location, DateOnly today, CancellationToken cancellationToken)
    {
        var yesterday = Acis.Date(today.AddDays(-1));
        return Acis.FindNearestAsync(
            httpClientFactory,
            location.Latitude,
            location.Longitude,
            bbox => new JsonObject
            {
                ["bbox"] = bbox,
                ["sdate"] = yesterday,
                ["edate"] = yesterday,
                ["elems"] = new JsonArray(
                    new JsonObject { ["name"] = "maxt" },
                    new JsonObject { ["name"] = "mint" },
                    AcisClimateNormals.NormalElement("maxt"),
                    AcisClimateNormals.NormalElement("mint")),
                ["meta"] = "name,sids,ll,valid_daterange",
            },
            station => IsSuitable(station, today),
            cancellationToken);
    }

    // Normals come back in the data, and the measured highs' and lows' periods in valid_daterange.
    private static bool IsSuitable(JsonElement station, DateOnly today)
    {
        var data = station.GetProperty("data");
        if (!Acis.IsPresent(data[2]) || !Acis.IsPresent(data[3])
            || !station.GetProperty("meta").TryGetProperty("valid_daterange", out var ranges))
        {
            return false;
        }

        var startedBy = today.AddYears(-MinimumRecordYears);
        var activeSince = today.AddDays(-ActiveWithinDays);
        return ranges.GetArrayLength() >= 2 && Enumerable.Range(0, 2).All(index =>
            ranges[index] is { ValueKind: JsonValueKind.Array } range && range.GetArrayLength() == 2
            && ParseDate(range[0]) is { } start && start <= startedBy
            && ParseDate(range[1]) is { } end && end >= activeSince);
    }

    private async Task<Records> GetRecordsAsync(AcisStation station, DateOnly today, CancellationToken cancellationToken)
    {
        var yesterday = today.AddDays(-1);
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
            ["edate"] = Acis.Date(yesterday),
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
            && ranges.GetArrayLength() > 0 && ranges[0].GetArrayLength() == 2
            && ParseDate(ranges[0][0]) is { } start
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
            : (value, ParseDate(group[1])?.Year);
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
            elements.Add(AcisClimateNormals.NormalElement(element));
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
            if (ParseDate(day[0]) is { } date)
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

    // ACIS sends values as text: "M" is missing, "T" a trace, "S" part of a later total, and a trailing letter is a flag.
    private static double? Number(JsonElement value)
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

        var end = text.Length;
        while (end > 0 && char.IsLetter(text[end - 1]))
        {
            end--;
        }

        return double.TryParse(text.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static DateOnly? ParseDate(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private sealed record Records(
        int? Since,
        IReadOnlyList<IReadOnlyList<(double? Value, int? Year)>> Days);
}
