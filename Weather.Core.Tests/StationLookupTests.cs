using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class StationLookupTests
{
    private const string Id = "KMETEST1";

    private const string Near = """
        {"location":{
            "stationId":["KFIRST1","KSECOND2","KTHIRD3"],
            "stationName":["First","Topsham","Brunswick"],
            "distanceMi":[0.5,1.9,2.4],
            "updateTimeUtc":[1,1,1],
            "latitude":[43.9,43.91,43.92],
            "longitude":[-69.9,-69.91,-69.92]
        }}
        """;

    private const string NoTemperature = """
        {"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","imperial":{"temp":null,"pressure":30}}]}
        """;

    private const string OldReading = """
        {"observations":[{"obsTimeUtc":"2026-10-30T14:30:00Z","imperial":{"temp":50}}]}
        """;

    private const string Current = """
        {"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","neighborhood":"Harpswell","lat":43.1234,"lon":-69.5678,"imperial":{"temp":50}}]}
        """;

    private static IOptions<WeatherOptions> Options(string? key = "secret-wu") =>
        TestData.Options(options => options.WeatherUnderground.ApiKey = key);

    private static StationDirectory Directory(StubHttp http, IOptions<WeatherOptions> options) => new(
        [
            new AmbientWeatherStation(http, options, new FixedTime(TestData.Now)),
            new WeatherUndergroundStation(new WeatherStationOptions { Id = "KCONFIG1", Name = "Configured" }, http, options),
        ],
        http,
        options);

    private static StationLookup Lookup(StubHttp http, string? key = "secret-wu")
    {
        var options = Options(key);
        return new StationLookup(Directory(http, options), http, new WeatherCache(), options, new FixedTime(TestData.Now));
    }

    private static StubHttp Respond(string body) => StubHttp.Json(_ => body);

    private static StubHttp RespondNear(string near, Func<string, string>? current = null) =>
        StubHttp.Json(url => url.Contains("location/near") ? near : current?.Invoke(url) ?? Current);

    [Fact]
    public async Task ZipFindsTheClosestStationEvenWithAnOldUpdateTime()
    {
        var http = RespondNear(Near);

        var match = await Lookup(http).FindAsync("04086");

        Assert.Equal(new StationMatch("KFIRST1", "First", 0.5), match);
        Assert.Contains("postalKey=04086:US", Uri.UnescapeDataString(http.Requests[0].RequestUri!.Query));
    }

    [Theory]
    [InlineData(NoTemperature)]
    [InlineData(OldReading)]
    [InlineData("")]
    public async Task ZipSkipsStationsThatCantBeUsed(string first)
    {
        var http = RespondNear(Near, url => url.Contains("KFIRST1") ? first : Current);

        var match = await Lookup(http).FindAsync("04086");

        Assert.Equal(new StationMatch("KSECOND2", "Topsham", 1.9), match);
    }

    [Fact]
    public async Task ZipStopsAtTheFirstUsableStation()
    {
        var http = RespondNear(Near);

        await Lookup(http).FindAsync("04086");

        Assert.Equal(2, http.Requests.Count);
    }

    [Theory]
    [InlineData(NoTemperature)]
    [InlineData(OldReading)]
    [InlineData("")]
    public async Task ZipWithNoUsableStationsHasNoMatch(string reading) =>
        Assert.Null(await Lookup(RespondNear(Near, _ => reading)).FindAsync("04086"));

    [Fact]
    public async Task ZipWithEmptyResponseHasNoMatch() =>
        Assert.Null(await Lookup(Respond("")).FindAsync("04086"));

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task UnrecognisedZipHasNoMatch(HttpStatusCode status)
    {
        var http = new StubHttp(_ => new HttpResponseMessage(status));

        Assert.Null(await Lookup(http).FindAsync("00000"));
    }

    [Fact]
    public async Task ZipResultsAreCached()
    {
        var http = RespondNear(Near);
        var lookup = Lookup(http);

        await lookup.FindAsync("04086");
        var requests = http.Requests.Count;
        await lookup.FindAsync("04086");

        Assert.Equal(requests, http.Requests.Count);
    }

    [Fact]
    public async Task IdUsesTheStationsPublicName()
    {
        var match = await Lookup(Respond(Current)).FindAsync(Id);

        Assert.Equal(new StationMatch(Id, "Harpswell", null), match);
    }

    [Fact]
    public async Task ConfiguredIdUsesItsConfiguredName()
    {
        var match = await Lookup(Respond(Current)).FindAsync("KCONFIG1");

        Assert.Equal(new StationMatch("KCONFIG1", "Configured", null), match);
    }

    [Fact]
    public async Task IdWithoutRecentDataHasNoMatch() =>
        Assert.Null(await Lookup(Respond("")).FindAsync(Id));

    [Fact]
    public async Task HomeIsNotALookupResult() =>
        Assert.Null(await Lookup(Respond(Current)).FindAsync("home"));

    [Theory]
    [InlineData("04086", true)]
    [InlineData("KMEBANGO86", true)]
    [InlineData("ab", false)]
    [InlineData("KME BANGO", false)]
    [InlineData("KME/../x", false)]
    [InlineData("", false)]
    public void ChecksQueries(string query, bool valid) =>
        Assert.Equal(valid, StationLookup.IsValidQuery(query));

    [Fact]
    public async Task MissingKeyIsReported()
    {
        var http = Respond(Near);

        await Assert.ThrowsAsync<StationLookupUnavailableException>(() => Lookup(http, key: null).FindAsync("04086"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task ServiceFailureIsReported()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<StationLookupUnavailableException>(() => Lookup(http).FindAsync("04086"));

        Assert.Equal("Station lookup failed. Source returned HTTP 503", exception.Message);
    }

    [Fact]
    public async Task MatchesNeverContainPrivateFields()
    {
        var json = JsonSerializer.Serialize(await Lookup(RespondNear(Near)).FindAsync("04086"));

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("43.9", json);
        Assert.DoesNotContain("69.9", json);
    }

    [Fact]
    public void DirectoryPrefersConfiguredStations()
    {
        var options = Options();
        var directory = Directory(Respond(""), options);

        Assert.Equal("Home station", directory.Find(null)!.Name);
        Assert.Equal("Configured", directory.Find("kconfig1")!.Name);
        Assert.Equal("Weather Underground", directory.Find(Id)!.Source);
        Assert.Equal(Id, directory.Find(Id)!.Name);
        Assert.Null(directory.Find("not valid!"));
        Assert.True(directory.IsConfigured("KCONFIG1"));
        Assert.False(directory.IsConfigured(Id));
    }
}
