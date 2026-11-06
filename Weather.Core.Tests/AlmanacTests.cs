using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather.Core.Almanac;
using Weather.Core.Models;

namespace Weather.Core.Tests;

public class AlmanacTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);
    private static readonly DateOnly Yesterday = new(2026, 10, 29);
    private static readonly DateOnly Tomorrow = new(2026, 10, 31);

    private const string NoStations = """{"data":[]}""";

    // Only Durham reports, has 30 years of records and has normals.
    private const string Search = """
        {"data":[
          {"meta":{"ll":[-69.93,43.9],"sids":["14611 1"],"name":"BRUNSWICK","valid_daterange":[["1945-02-01","2010-01-31"],["1945-02-01","2010-01-31"],[],[]]},"data":["M","M","50","33"]},
          {"meta":{"ll":[-69.86,43.93],"sids":["170409 2"],"name":"BATH","valid_daterange":[["2000-01-01","2026-10-29"],["2000-01-01","2026-10-29"],[],[]]},"data":["67","43","51","31"]},
          {"meta":{"ll":[-69.97,43.93],"sids":["NONORMAL 2"],"name":"NO NORMALS","valid_daterange":[["1900-01-01","2026-10-29"],["1900-01-01","2026-10-29"],[],[]]},"data":["67","43","M","M"]},
          {"meta":{"ll":[-69.966,43.929],"sids":["NORANGE 2"],"name":"NO PERIOD"},"data":["67","43","50","30"]},
          {"meta":{"ll":[-70.1,44.0],"sids":["172048 2"],"name":"DURHAM","valid_daterange":[["1994-09-01","2026-10-27"],["1994-09-01","2026-10-29"],[],[]]},"data":["73","41","50","30"]},
          {"meta":{"ll":[-70.3,43.64],"sids":["14764 1"],"name":"PORTLAND JETPORT","valid_daterange":[["1940-11-07","2026-10-29"],["1940-11-07","2026-10-29"],[],[]]},"data":["66","54","52","34"]}
        ]}
        """;

    // Yesterday's four records, then today's and tomorrow's: high, low, precipitation and snow.
    private const string Records = """
        {"meta":{"valid_daterange":[["1994-09-01","2026-10-29"]]},
         "smry":[
           [["72","2017-10-29"]],[["18","2001-10-29"]],[["2.59","1985-10-29"]],[["T","2008-10-29"]],
           [["79","2024-10-30"]],[["20","1953-10-30"]],[["1.90","1990-10-30"]],[["0.0","2025-10-30"]],
           [["77","1999-10-31"]],[["19","2002-10-31"]],[["1.20","2011-10-31"]],[["M",""]]
         ]}
        """;

    // Measured high, low, precipitation and snow, then their normals. {0} is yesterday's precipitation.
    private const string DailyTemplate = """
        {"data":[
          ["2026-10-29","72","53","{0}","S","52","35","0.14","0.0"],
          ["2026-10-30","M","M","M","M","52","34","0.15","M"],
          ["2026-10-31","M","M","M","M","51","34","0.15","0.1"]
        ]}
        """;

    private static readonly string Daily = DailyTemplate.Replace("{0}", "T");

    private const string Outlook = """
        {
            "timezone": "America/New_York",
            "utc_offset_seconds": -14400,
            "daily": {
                "time": ["2026-10-30", "2026-10-31"],
                "weather_code": [3, 71],
                "temperature_2m_max": [66.4, 61.6],
                "temperature_2m_min": [51.2, 45],
                "sunrise": ["2026-10-30T07:14", "2026-10-31T07:15"],
                "sunset": ["2026-10-30T17:31", "2026-10-31T17:30"],
                "precipitation_sum": [0.94, 0.2],
                "snowfall_sum": [0, 0.4],
                "precipitation_probability_max": [80, 40],
                "wind_speed_10m_max": [13.1, 9],
                "wind_direction_10m_dominant": [322, 300],
                "uv_index_max": [3.45, 2]
            },
            "hourly": {
                "time": ["2026-10-30T11:00"],
                "weather_code": [3],
                "is_day": [1],
                "temperature_2m": [60],
                "apparent_temperature": [58],
                "precipitation_probability": [80],
                "precipitation": [0.1],
                "snowfall": [0],
                "cloud_cover": [90],
                "dew_point_2m": [50],
                "relative_humidity_2m": [70],
                "wind_speed_10m": [10],
                "wind_direction_10m": [300],
                "pressure_msl": [1010]
            }
        }
        """;

    /// <summary>Stubbed ACIS and Open-Meteo answers. <see cref="Search"/> gets the zero-based search attempt.</summary>
    private sealed record Stubs(
        Func<int, string>? Search = null,
        string Records = Records,
        string? Daily = null,
        bool AcisFails = false,
        bool ForecastFails = false);

    private static (ServiceProvider Services, StubHttp Http) Services(Stubs? stubs = null, DateTimeOffset? now = null)
    {
        stubs ??= new Stubs();
        var searches = 0;
        var http = new StubHttp(request =>
        {
            var url = request.RequestUri!.ToString();
            var isAcis = url.Contains("rcc-acis.org");
            if ((isAcis && stubs.AcisFails) || (url.Contains("open-meteo.com") && stubs.ForecastFails))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var body = !isAcis ? Outlook
                : url.EndsWith("MultiStnData") ? (stubs.Search ?? (_ => Search))(searches++)
                : request.Content!.ReadAsStringAsync().Result.Contains("\"por\"") ? stubs.Records
                : stubs.Daily ?? Daily;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTime(now ?? TestData.Now));
        services.AddWeatherCore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultLocation:Zip"] = "04086",
            ["DefaultLocation:Latitude"] = "43.9281",
            ["DefaultLocation:Longitude"] = "-69.9656",
            ["DefaultLocation:TimeZoneId"] = "America/New_York",
        }).Build());
        services.AddSingleton<IHttpClientFactory>(http);
        return (services.BuildServiceProvider(), http);
    }

    private static List<string> AcisBodies(StubHttp http, string endpoint) =>
        http.Requests
            .Where(request => request.RequestUri!.AbsolutePath == "/" + endpoint)
            .Select(request => request.Content!.ReadAsStringAsync().Result)
            .ToList();

    [Fact]
    public async Task CombinesForecastAveragesAndRecords()
    {
        var (services, http) = Services();
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal("DURHAM", report.ClimateStation);
        Assert.Equal(1994, report.RecordsSince);
        Assert.Equal(Today, report.Today);
        Assert.Equal([Yesterday, Today, Tomorrow], report.Days.Select(day => day.Date));

        var today = report.Days[1];
        Assert.Equal(new AlmanacRow(66, 52, 79, 2024), today.High);
        Assert.Equal(new AlmanacRow(51, 34, 20, 1953), today.Low);
        Assert.Equal(new AlmanacRow(0.94, 0.15, 1.90, 1990), today.Precip);
        // No snow has fallen on this date, so the record has no year.
        Assert.Equal(new AlmanacRow(0, null, 0, null), today.Snow);

        var yesterday = report.Days[0];
        Assert.Equal(new AlmanacRow(72, 52, 72, 2017), yesterday.High);
        Assert.Equal(new AlmanacRow(53, 35, 18, 2001), yesterday.Low);
        Assert.Equal(new AlmanacRow(AlmanacRow.TraceInches, 0.14, 2.59, 1985), yesterday.Precip);
        // "S" means the amount is in a later total, so yesterday's snow is unknown.
        Assert.Equal(new AlmanacRow(null, 0, AlmanacRow.TraceInches, 2008), yesterday.Snow);

        // Tomorrow also shows the forecast.
        var tomorrow = report.Days[2];
        Assert.Equal(new AlmanacRow(62, 51, 77, 1999), tomorrow.High);
        Assert.Equal(new AlmanacRow(45, 34, 19, 2002), tomorrow.Low);
        Assert.Equal(new AlmanacRow(0.2, 0.15, 1.20, 2011), tomorrow.Precip);
        Assert.Equal(new AlmanacRow(0.4, 0.1, null, null), tomorrow.Snow);

        // Records run to yesterday and are grouped by each day of the year.
        var records = Assert.Single(AcisBodies(http, "StnData"), body => body.Contains("\"por\""));
        Assert.Contains("172048 2", records);
        Assert.Contains("\"edate\":\"2026-10-29\"", records);
        Assert.Contains("\"10-29\"", records);
        Assert.Contains("\"10-30\"", records);
        Assert.Contains("\"10-31\"", records);
    }

    [Theory]
    [InlineData("0.52", 0.52)]
    [InlineData("T", AlmanacRow.TraceInches)]
    [InlineData("M", null)]
    [InlineData("S", null)]
    [InlineData("3.39A", null)]
    public async Task AccumulatedAndMissingAmountsAreNotOneDaysTotal(string measured, double? expected)
    {
        var (services, _) = Services(new Stubs(Daily: DailyTemplate.Replace("{0}", measured)));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(expected, report.Days[0].Precip.Value);
    }

    [Fact]
    public async Task MissingPeriodOfRecordLeavesRecordsUndated()
    {
        const string records = """
            {"smry":[
              [["72","2017-10-29"]],[["18","2001-10-29"]],[["2.59","1985-10-29"]],[["T","2008-10-29"]],
              [["79","2024-10-30"]],[["20","1953-10-30"]],[["1.90","1990-10-30"]],[["0.0","2025-10-30"]],
              [["77","1999-10-31"]],[["19","2002-10-31"]],[["1.20","2011-10-31"]],[["M",""]]
            ]}
            """;
        var (services, _) = Services(new Stubs(Records: records));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Null(report.RecordsSince);
        Assert.Equal(79, report.Days[1].High.Record);
    }

    [Fact]
    public async Task WidensTheSearchUntilAStationQualifies()
    {
        var (services, http) = Services(new Stubs(Search: attempt => attempt < 2 ? NoStations : Search));
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal("DURHAM", report.ClimateStation);
        var searches = AcisBodies(http, "MultiStnData");
        Assert.Equal(3, searches.Count);
        Assert.Equal(3, searches.Distinct().Count());
    }

    [Fact]
    public async Task NoStationIsRememberedWithItsOwnMessage()
    {
        var (services, http) = Services(new Stubs(Search: _ => NoStations));
        using var _ = services;
        var almanac = services.GetRequiredService<AlmanacService>();

        var report = (await almanac.GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("No climate station nearby has averages and records.", report.Message);
        Assert.Equal(4, AcisBodies(http, "MultiStnData").Count);

        // The forecast table's averages share the remembered answer, so they don't search again.
        await Assert.ThrowsAsync<Climate.NoClimateStationException>(() => services.GetRequiredService<Climate.IClimateNormals>()
            .GetAsync(TestData.Topsham, Today, Tomorrow, default));
        Assert.Equal(4, AcisBodies(http, "MultiStnData").Count);
    }

    [Fact]
    public async Task ForecastTableAveragesUseTheAlmanacsStation()
    {
        var (services, http) = Services();
        using var _ = services;

        await services.GetRequiredService<AlmanacService>().GetAsync();
        var normals = await services.GetRequiredService<Climate.IClimateNormals>()
            .GetAsync(TestData.Topsham, Today, Tomorrow, default);

        Assert.Equal("DURHAM", normals.Station);
        Assert.Single(AcisBodies(http, "MultiStnData"));
    }

    [Theory]
    [InlineData(2027, 1, 1, "12-31", "01-01", "01-02", "2026-12-31", "2027-01-02")]
    [InlineData(2028, 2, 29, "02-28", "02-29", "03-01", "2028-02-28", "2028-03-01")]
    [InlineData(2027, 3, 1, "02-28", "03-01", "03-02", "2027-02-28", "2027-03-02")]
    public async Task DatesCrossYearsAndLeapDays(
        int year, int month, int day, string before, string on, string after, string start, string end)
    {
        // Noon in Maine, so the forecast's today and the fallback agree.
        var now = new DateTimeOffset(year, month, day, 17, 0, 0, TimeSpan.Zero);
        // The stations must still be reporting on these later dates.
        var search = Search.Replace("2026-10-27", "2030-12-31").Replace("2026-10-29", "2030-12-31");
        var (services, http) = Services(new Stubs(Search: _ => search, ForecastFails: true), now);
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(new DateOnly(year, month, day), report.Today);
        var records = Assert.Single(AcisBodies(http, "StnData"), body => body.Contains("\"por\""));
        Assert.Contains($"[\"year\",\"{before}\",\"{before}\"]", records);
        Assert.Contains($"[\"year\",\"{on}\",\"{on}\"]", records);
        Assert.Contains($"[\"year\",\"{after}\",\"{after}\"]", records);
        Assert.Contains($"\"edate\":\"{start}\"", records);

        var daily = Assert.Single(AcisBodies(http, "StnData"), body => !body.Contains("\"por\""));
        Assert.Contains($"\"sdate\":\"{start}\"", daily);
        Assert.Contains($"\"edate\":\"{end}\"", daily);
    }

    [Fact]
    public async Task CachesTheClimateData()
    {
        var (services, http) = Services();
        using var _ = services;
        var almanac = services.GetRequiredService<AlmanacService>();

        await almanac.GetAsync();
        var calls = http.Requests.Count;
        await almanac.GetAsync();

        Assert.Equal(calls, http.Requests.Count);
    }

    [Fact]
    public async Task ClimateFailureIsAnError()
    {
        var (services, _) = Services(new Stubs(AcisFails: true));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("Source returned HTTP 500", report.Message);
        Assert.Null(report.Today);
        Assert.Empty(report.Days);
    }

    [Fact]
    public async Task ForecastFailureOnlyBlanksTheForecast()
    {
        var (services, _) = Services(new Stubs(ForecastFails: true));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal(Today, report.Today);
        Assert.Equal(new AlmanacRow(null, 52, 79, 2024), report.Days[1].High);
        Assert.Null(report.Days[2].High.Value);
        Assert.Equal(72, report.Days[0].High.Value);
    }

    [Fact]
    public async Task BadIdIsNullAndUnkeyedStationNeedsKey()
    {
        var (services, http) = Services();
        using var _ = services;
        var almanac = services.GetRequiredService<AlmanacService>();

        Assert.Null(await almanac.GetAsync("not/valid"));
        Assert.Equal(SourceStatus.NeedsKey, (await almanac.GetAsync("KMETEST1"))!.Status);
        Assert.Empty(http.Requests);
    }
}
