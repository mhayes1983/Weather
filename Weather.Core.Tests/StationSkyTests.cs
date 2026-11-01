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

    private sealed class SlowSky : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (AsksForSky(request))
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Station(TestData.Now)) };
        }
    }

    [Fact]
    public async Task SlowSkyDoesNotHoldUpTheReport()
    {
        var http = new SlowSky();
        var options = TestData.Options(settings => settings.OpenMeteoStations.SkyTimeoutMilliseconds = 50);
        var time = new FixedTime(TestData.Now);
        var cache = new WeatherCache();
        var service = new StationService(
            new StationDirectory([], http, options, TestData.Locations(http, options), time),
            cache,
            options,
            time,
            TestData.Sky(http, options, cache),
            NullLogger<StationService>.Instance);

        var report = await service.GetReportAsync(Id).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(StationStatus.Ok, report!.Status);
        Assert.Null(report.Sky);
    }

    [Theory]
    [InlineData("kmebruns32", "KMEBRUNS32")]
    [InlineData("KMEBRUNS32", "KMEBRUNS32")]
    public void TypedDefaultsTakeTheirConfiguredSpelling(string typed, string expected)
    {
        var (defaults, added) = StationSelection.Split([typed], ["KMEBRUNS32", "KMEBANGO86"]);

        Assert.Equal([expected], defaults);
        Assert.Empty(added);
    }

    [Fact]
    public void SelectionDropsDuplicatesAndBadIdsAndKeepsAddedStations()
    {
        var (defaults, added) = StationSelection.Split(
            ["KMEBRUNS32", "kmebruns32", "not/valid", "KOTHER1", "kother1"],
            ["KMEBRUNS32"]);

        Assert.Equal(["KMEBRUNS32"], defaults);
        Assert.Equal(["KOTHER1"], added);
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
