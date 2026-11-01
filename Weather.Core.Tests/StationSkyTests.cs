using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class StationSkyTests
{
    private const string Id = "om-04086";

    private static string Station(DateTimeOffset observed) => $$"""
        {
          "current": { "time": {{observed.ToUnixTimeSeconds()}}, "interval": 900, "temperature_2m": 40 },
          "hourly": { "time": [{{observed.ToUnixTimeSeconds()}}], "temperature_2m": [40] }
        }
        """;

    private static bool AsksForSky(HttpRequestMessage request) => request.RequestUri!.Query.Contains("weather_code");

    private static StubHttp Http(string sky, DateTimeOffset? observed = null) =>
        StubHttp.Json(url => url.Contains("weather_code") ? sky : Station(observed ?? TestData.Now));

    private static StationService Service(StubHttp http)
    {
        var options = TestData.Options();
        var time = new FixedTime(TestData.Now);
        return new StationService(
            new StationDirectory([], http, options, TestData.Locations(http, options), time),
            new WeatherCache(),
            options,
            time,
            TestData.Sky(http, options),
            NullLogger<StationService>.Instance);
    }

    [Fact]
    public async Task DaytimeCodeIsDescribed()
    {
        var report = await Service(Http("""{"current":{"weather_code":2,"is_day":1}}""")).GetReportAsync(Id);

        Assert.Equal(new CurrentSky("Partly Cloudy", "partly-cloudy"), report!.Sky);
    }

    [Fact]
    public async Task NightUsesTheNightIcon()
    {
        var report = await Service(Http("""{"current":{"weather_code":0,"is_day":0}}""")).GetReportAsync(Id);

        Assert.Equal(new CurrentSky("Clear", "clear-night"), report!.Sky);
    }

    [Fact]
    public async Task MissingCodeHidesTheSky()
    {
        var report = await Service(Http("""{"current":{"weather_code":null,"is_day":1}}""")).GetReportAsync(Id);

        Assert.Equal(StationStatus.Ok, report!.Status);
        Assert.Null(report.Sky);
    }

    [Fact]
    public async Task FailureHidesTheSkyButKeepsTheReadings()
    {
        var http = new StubHttp(request => AsksForSky(request)
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Station(TestData.Now)) });

        var report = await Service(http).GetReportAsync(Id);

        Assert.Equal(StationStatus.Ok, report!.Status);
        Assert.NotNull(report.Current);
        Assert.Null(report.Sky);
    }

    [Fact]
    public async Task OfflineStationHasNoSky()
    {
        var http = Http("""{"current":{"weather_code":2,"is_day":1}}""", TestData.Now.AddDays(-1));

        var report = await Service(http).GetReportAsync(Id);

        Assert.Equal(StationStatus.Offline, report!.Status);
        Assert.Null(report.Sky);
        Assert.DoesNotContain(http.Requests, AsksForSky);
    }

    [Fact]
    public async Task RepeatRequestsUseTheCache()
    {
        var http = Http("""{"current":{"weather_code":2,"is_day":1}}""");
        var service = Service(http);

        await service.GetReportAsync(Id);
        await service.GetReportAsync(Id);

        Assert.Single(http.Requests, AsksForSky);
    }
}
