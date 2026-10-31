using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;
using Weather.Server.Controllers;

namespace Weather.Core.Tests;

public class StationControllerTests
{
    private const string Near = """
        {"location":{"stationId":["KFIRST1"],"stationName":["First"],"distanceMi":[0.5]}}
        """;

    private const string Current = """
        {"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","neighborhood":"Harpswell","imperial":{"temp":50}}]}
        """;

    private static IOptions<WeatherOptions> Options(string? key = "secret-wu") =>
        TestData.Options(options => options.WeatherUnderground.ApiKey = key);

    private static StationController Controller(StubHttp http, string? key = "secret-wu")
    {
        var options = Options(key);
        var directory = new StationDirectory([], http, options);
        var cache = new WeatherCache();
        var time = new FixedTime(TestData.Now);
        var stations = new StationService(directory, cache, options, time, NullLogger<StationService>.Instance);
        var lookup = new StationLookup(directory, stations, http, cache, options, time, NullLogger<StationLookup>.Instance);
        return new StationController(stations, lookup);
    }

    private static ProblemDetails Problem(ActionResult<StationMatch> result, int status)
    {
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(status, objectResult.StatusCode);
        return Assert.IsType<ProblemDetails>(objectResult.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("KME/../x")]
    public async Task BadQueryIs400(string? query)
    {
        var http = StubHttp.Json(_ => Near);

        var problem = Problem(await Controller(http).LookUp(query), 400);

        Assert.Equal("Enter a 5-digit ZIP code or a station ID.", problem.Detail);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task ZipWithNoMatchIs404()
    {
        var problem = Problem(await Controller(StubHttp.Json(_ => "")).LookUp("04086"), 404);

        Assert.Equal("No station near that ZIP code has reported in the last hour.", problem.Detail);
    }

    [Fact]
    public async Task IdWithNoMatchIs404()
    {
        var problem = Problem(await Controller(StubHttp.Json(_ => "")).LookUp("KMETEST1"), 404);

        Assert.Equal("No station with that ID has reported in the last hour. Station IDs are case sensitive.", problem.Detail);
    }

    [Fact]
    public async Task MissingKeyIs503() =>
        Problem(await Controller(StubHttp.Json(_ => Near), key: null).LookUp("04086"), 503);

    [Fact]
    public async Task ServiceFailureIs503()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var problem = Problem(await Controller(http).LookUp("04086"), 503);

        Assert.Equal("Station lookup failed. Source returned HTTP 429", problem.Detail);
    }

    [Fact]
    public async Task UnreadableResponseIs503() =>
        Problem(await Controller(StubHttp.Json(_ => "{}")).LookUp("04086"), 503);

    [Fact]
    public async Task MatchIsReturnedAfterTrimmingTheQuery()
    {
        var http = StubHttp.Json(url => url.Contains("location/near") ? Near : Current);

        var result = await Controller(http).LookUp(" 04086 ");

        Assert.Equal(new StationMatch("KFIRST1", "First", 0.5), result.Value);
    }
}
