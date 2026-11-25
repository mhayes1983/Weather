using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather.Core.Almanac;
using Weather.Core.DailyForecast;
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
                "time": ["2026-10-28", "2026-10-29", "2026-10-30", "2026-10-31"],
                "weather_code": [0, 1, 3, 71],
                "temperature_2m_max": [70, 71, 66.4, 61.6],
                "temperature_2m_min": [50, 52, 51.2, 45],
                "sunrise": ["2026-10-28T07:11", "2026-10-29T07:12", "2026-10-30T07:14", "2026-10-31T07:15"],
                "sunset": ["2026-10-28T17:34", "2026-10-29T17:33", "2026-10-30T17:31", "2026-10-31T17:30"],
                "precipitation_sum": [0, 0, 0.94, 0.2],
                "snowfall_sum": [0, 0, 0, 0.4],
                "precipitation_probability_max": [0, 0, 80, 40],
                "wind_speed_10m_max": [5, 6, 13.1, 9],
                "wind_direction_10m_dominant": [200, 210, 322, 300],
                "uv_index_max": [3, 3, 3.45, 2]
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

    private const string Usno = """
        {"properties":{"data":{
            "curphase": "Waxing Gibbous",
            "fracillum": "78%",
            "moondata": [{"phen":"Rise","time":"15:02"}],
            "sundata": [{"phen":"Rise","time":"07:12"},{"phen":"Set","time":"17:33"}]
        }}}
        """;

    // Brunswick is under a mile from Topsham. Portland is too far to use.
    private const string TideStations = """
        {"stations":[
          {"id":"8418150","name":"Portland","lat":43.6567,"lng":-70.2467,"type":"R"},
          {"id":"8417527","name":"Brunswick, Androscoggin River","lat":43.9183,"lng":-69.9633,"type":"S"},
          {"id":"BADROW","name":"No position"}
        ]}
        """;

    private const string FarTideStations = """{"stations":[{"id":"8418150","name":"Portland","lat":43.6567,"lng":-70.2467,"type":"R"}]}""";

    private const string Tides = """
        { "predictions" : [
        {"t":"2026-10-29 11:02", "v":"0.104", "type":"L"},
        {"t":"2026-10-30 00:23", "v":"4.009", "type":"H"},{"t":"2026-10-30 08:18", "v":"0.005", "type":"L"},
        {"t":"2026-10-30 12:31", "v":"4.698", "type":"H"},{"t":"2026-10-30 21:00", "v":"-0.548", "type":"L"},
        {"t":"2026-10-31 01:13", "v":"4.075", "type":"X"}
        ]}
        """;

    // June 19 to 23, around the solstice before the test's today. The archive gives every date today's
    // UTC offset, so these read an hour early, which doesn't change daylight. The longest is 928 minutes.
    private const string Archive = """
        {"utc_offset_seconds": -14400, "daily": {
            "time": ["2026-06-19", "2026-06-20", "2026-06-21", "2026-06-22", "2026-06-23"],
            "sunrise": ["2026-06-19T03:57", "2026-06-20T03:57", "2026-06-21T03:58", "2026-06-22T03:58", "2026-06-23T03:58"],
            "sunset": ["2026-06-19T19:24", "2026-06-20T19:25", "2026-06-21T19:25", "2026-06-22T19:25", "2026-06-23T19:25"]
        }}
        """;

    private const string NoTides = """{"error": {"message":"No Predictions data was found."}}""";

    /// <summary>Stubbed ACIS, Open-Meteo, USNO and NOAA answers. <see cref="Search"/> gets the zero-based search attempt.</summary>
    private sealed record Stubs(
        Func<int, string>? Search = null,
        string Records = Records,
        string? Daily = null,
        bool AcisFails = false,
        bool ForecastFails = false,
        bool UsnoFails = false,
        string Outlook = Outlook,
        string TideStations = TideStations,
        string Tides = Tides,
        bool TidesFail = false,
        bool ArchiveFails = false);

    /// <summary>A clock a test can move on, to see what's kept from one day to the next.</summary>
    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ServiceProvider Services, StubHttp Http) Services(Stubs? stubs = null, DateTimeOffset? now = null, TimeProvider? time = null)
    {
        stubs ??= new Stubs();
        var searches = 0;
        var http = new StubHttp(request =>
        {
            var url = request.RequestUri!.ToString();
            var isAcis = url.Contains("rcc-acis.org");
            var isUsno = url.Contains("aa.usno.navy.mil");
            var isNoaa = url.Contains("tidesandcurrents.noaa.gov");
            var isArchive = url.Contains("archive-api.open-meteo.com");
            if ((isArchive && stubs.ArchiveFails) || (isAcis && stubs.AcisFails) || (url.Contains("open-meteo.com") && stubs.ForecastFails) || (isUsno && stubs.UsnoFails) || (isNoaa && stubs.TidesFail))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var body = isUsno ? Usno
                : isArchive ? Archive
                : isNoaa ? (url.Contains("stations.json") ? stubs.TideStations : stubs.Tides)
                : !isAcis ? stubs.Outlook
                : url.EndsWith("MultiStnData") ? (stubs.Search ?? (_ => Search))(searches++)
                : request.Content!.ReadAsStringAsync().Result.Contains("\"por\"") ? stubs.Records
                : stubs.Daily ?? Daily;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(time ?? new FixedTime(now ?? TestData.Now));
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

    [Fact]
    public async Task EachDayHasSunAndMoon()
    {
        var (services, http) = Services();
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        var moon = new MoonInfo("Waxing Gibbous", 78, "15:02", null);
        // Sun times match the daily forecast, whose past days cover yesterday and the day before.
        Assert.Equal(new AlmanacSky("07:12", "17:33", moon, 621, -2, -307), report.Days[0].Sky);
        Assert.Equal(new AlmanacSky("07:14", "17:31", moon, 617, -4, -311), report.Days[1].Sky);
        Assert.Equal(new AlmanacSky("07:15", "17:30", moon, 615, -2, -313), report.Days[2].Sky);
        Assert.Equal(3, http.Requests.Count(request => request.RequestUri!.Host == "aa.usno.navy.mil"));
        var archive = Assert.Single(http.Requests, request => request.RequestUri!.Host == "archive-api.open-meteo.com");
        Assert.Contains("start_date=2026-06-19&end_date=2026-06-23", archive.RequestUri!.Query);
        Assert.Contains(http.Requests, request => request.RequestUri!.Query.Contains("date=2026-10-29"));
    }

    [Fact]
    public async Task MissingForecastSunTimesFallBackToUsno()
    {
        var outlook = Outlook.Replace("\"2026-10-31T07:15\"", "null");
        var (services, _) = Services(new Stubs(Outlook: outlook));
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal("07:12", report.Days[2].Sky!.Sunrise);
        Assert.Equal("17:30", report.Days[2].Sky!.Sunset);
        Assert.Equal(618, report.Days[2].Sky!.DaylightMinutes);
        Assert.Equal("07:14", report.Days[1].Sky!.Sunrise);
    }

    [Fact]
    public async Task DaylightChangeNeedsTheDayBefore()
    {
        // Yesterday's sun falls back to USNO, but nothing knows the day before.
        var outlook = Outlook.Replace("\"2026-10-28T07:11\"", "null").Replace("\"2026-10-29T07:12\"", "null");
        var (services, _) = Services(new Stubs(Outlook: outlook));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(621, report.Days[0].Sky!.DaylightMinutes);
        Assert.Null(report.Days[0].Sky!.DaylightChangeMinutes);
        Assert.Equal(-4, report.Days[1].Sky!.DaylightChangeMinutes);
    }

    [Theory]
    [InlineData("2026-06-20", "2025-12-21")]
    [InlineData("2026-06-21", "2026-06-21")]
    [InlineData("2026-12-20", "2026-06-21")]
    [InlineData("2026-12-21", "2026-12-21")]
    [InlineData("2027-01-05", "2026-12-21")]
    public void LastSolsticeIsTheMostRecentTurningPoint(string date, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), SolsticeDaylight.LastSolstice(DateOnly.Parse(date)));

    [Theory]
    [InlineData("2026-12-21", "2026-12-21")]
    [InlineData("2026-12-22", "2026-12-22")]
    [InlineData("2027-01-30", "2026-12-23")]
    public void SolsticeWindowEndsByToday(string today, string end) =>
        Assert.Equal(
            (new DateOnly(2026, 12, 19), DateOnly.Parse(end)),
            SolsticeDaylight.Window(new DateOnly(2026, 12, 21), DateOnly.Parse(today)));

    [Fact]
    public async Task WinterSolsticeUsesTheShortestDay()
    {
        const string december = """
            {"daily": {
                "time": ["2026-12-19", "2026-12-20", "2026-12-21", "2026-12-22"],
                "sunrise": ["2026-12-19T07:07", "2026-12-20T07:08", "2026-12-21T07:08", "2026-12-22T07:09"],
                "sunset": ["2026-12-19T16:03", "2026-12-20T16:03", "2026-12-21T16:04", "2026-12-22T16:05"]
            }}
            """;
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(december) });

        var reference = await new SolsticeDaylight(http).GetAsync(
            new GeoPoint(TestData.Topsham.Latitude, TestData.Topsham.Longitude), new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 19), new DateOnly(2026, 12, 22), default);

        Assert.Equal(new SolsticeReference(535), reference);
    }

    [Fact]
    public async Task SolsticeWithoutSunriseHasNoDaylight()
    {
        const string polar = """{"daily": {"time": ["2026-12-21"], "sunrise": [null], "sunset": [null]}}""";
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(polar) });

        var reference = await new SolsticeDaylight(http).GetAsync(
            new GeoPoint(TestData.Topsham.Latitude, TestData.Topsham.Longitude), new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 21), new DateOnly(2026, 12, 21), default);

        Assert.Null(reference.DaylightMinutes);
    }

    [Fact]
    public async Task ArchiveFailureOnlyDropsTheTotal()
    {
        var (services, _) = Services(new Stubs(ArchiveFails: true));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.All(report.Days, day => Assert.Null(day.Sky!.DaylightSinceSolsticeMinutes));
        Assert.Equal(617, report.Days[1].Sky!.DaylightMinutes);
        Assert.Equal(-4, report.Days[1].Sky!.DaylightChangeMinutes);
    }

    [Theory]
    [InlineData("07:14", "17:31", 617)]
    [InlineData("05:01", "20:24", 923)]
    [InlineData(null, "17:31", null)]
    [InlineData("07:14", "bad", null)]
    [InlineData("17:31", "07:14", null)]
    // Open-Meteo's times keep their dates: Fairbanks' June sunset after midnight, then Utqiagvik's polar day and night.
    [InlineData("2026-06-21T02:57", "2026-06-22T00:47", 1310)]
    [InlineData("2026-06-21T00:00", "2026-06-22T00:00", 1440)]
    [InlineData("2026-11-25T00:00", "2026-11-25T00:00", 0)]
    [InlineData("2026-10-30T07:14", "2026-10-30T17:31", 617)]
    // A forecast time beside a USNO one is compared by the clock.
    [InlineData("2026-10-30T07:14", "17:31", 617)]
    [InlineData("2026-06-22T00:47", "2026-06-21T02:57", null)]
    public void DaylightIsSunsetMinusSunrise(string? sunrise, string? sunset, int? expected) =>
        Assert.Equal(expected, Weather.Core.DailyForecast.DailyForecastService.DaylightMinutes(sunrise, sunset));

    [Fact]
    public async Task EachDayHasTheNearestStationsTides()
    {
        var (services, http) = Services();
        using var _ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal("Brunswick, Androscoggin River", report.TideStation);
        Assert.Equal([new AlmanacTide("11:02", false, 0.1)], report.Days[0].Tides!);
        Assert.Equal(
            [new AlmanacTide("00:23", true, 4), new AlmanacTide("08:18", false, 0), new AlmanacTide("12:31", true, 4.7), new AlmanacTide("21:00", false, -0.5)],
            report.Days[1].Tides!);
        // An unknown type is skipped, which leaves tomorrow with none.
        Assert.Null(report.Days[2].Tides);

        var predictions = Assert.Single(http.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("datagetter"));
        var query = predictions.RequestUri!.Query;
        Assert.Contains("station=8417527", query);
        Assert.Contains("begin_date=20261029", query);
        Assert.Contains("end_date=20261031", query);
        Assert.Contains("interval=hilo", query);
        Assert.Contains("time_zone=lst_ldt", query);
    }

    [Fact]
    public async Task SunsetAfterMidnightStillCountsAsDaylight()
    {
        // Tomorrow's sun sets at 12:47 AM the day after, as in Fairbanks in June.
        var outlook = Outlook.Replace("\"2026-10-31T17:30\"", "\"2026-11-01T00:47\"");
        var (services, _) = Services(new Stubs(Outlook: outlook));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal("00:47", report.Days[2].Sky!.Sunset);
        Assert.Equal(1052, report.Days[2].Sky!.DaylightMinutes);
        Assert.Equal(435, report.Days[2].Sky!.DaylightChangeMinutes);
    }

    [Fact]
    public async Task NoTideStationIsLookedUpOnceAcrossDays()
    {
        var time = new MovableTime(TestData.Now);
        var (services, http) = Services(new Stubs(TideStations: FarTideStations), time: time);
        using var _ = services;
        var almanac = services.GetRequiredService<AlmanacService>();

        await almanac.GetAsync();
        time.Now = TestData.Now.AddDays(1);
        var report = (await almanac.GetAsync())!;

        // The dated tides entry is new each day, but the station answer is kept, so the list isn't fetched again.
        Assert.Equal(Tomorrow, report.Today);
        Assert.Null(report.TideStation);
        Assert.Single(http.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("stations.json"));
        Assert.DoesNotContain(http.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("datagetter"));
    }

    [Fact]
    public async Task NoTideStationNearbyHidesTides()
    {
        var (services, http) = Services(new Stubs(TideStations: FarTideStations));
        using var _ = services;
        var almanac = services.GetRequiredService<AlmanacService>();

        var report = (await almanac.GetAsync())!;
        await almanac.GetAsync();

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Null(report.TideStation);
        Assert.All(report.Days, day => Assert.Null(day.Tides));
        Assert.DoesNotContain(http.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("datagetter"));
        Assert.Single(http.Requests, request => request.RequestUri!.AbsolutePath.EndsWith("stations.json"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TideFailureOnlyHidesTides(bool fails, bool noPredictions)
    {
        var (services, _) = Services(new Stubs(TidesFail: fails, Tides: noPredictions ? NoTides : Tides));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Null(report.TideStation);
        Assert.All(report.Days, day => Assert.Null(day.Tides));
        Assert.Equal(66, report.Days[1].High.Value);
    }

    [Fact]
    public async Task UsnoFailureKeepsForecastSunTimes()
    {
        var (services, _) = Services(new Stubs(UsnoFails: true));
        using var __ = services;

        var report = (await services.GetRequiredService<AlmanacService>().GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal(new AlmanacSky("07:12", "17:33", null, 621, -2, -307), report.Days[0].Sky);
        Assert.Equal(new AlmanacSky("07:14", "17:31", null, 617, -4, -311), report.Days[1].Sky);
        Assert.Equal(new AlmanacSky("07:15", "17:30", null, 615, -2, -313), report.Days[2].Sky);
        Assert.Equal(66, report.Days[1].High.Value);
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
        Assert.All(report.Days, day => Assert.Null(day.Sky));
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
