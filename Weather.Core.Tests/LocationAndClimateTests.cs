using System.Net;
using Weather.Core.Climate;
using Weather.Core.Locations;

namespace Weather.Core.Tests;

public class LocationAndClimateTests
{
    [Theory]
    [InlineData("04086", true)]
    [InlineData("4086", false)]
    [InlineData("040860", false)]
    [InlineData("0408a", false)]
    public void ValidatesZipFormat(string zip, bool expected)
    {
        Assert.Equal(expected, LocationResolver.IsValidZip(zip));
    }

    [Fact]
    public async Task DefaultZipNeedsNoLookup()
    {
        var http = StubHttp.Json(_ => throw new InvalidOperationException("No request expected."));
        var resolver = new LocationResolver(http, new WeatherCache(), TestData.Options());

        Assert.Equal(TestData.Topsham, await resolver.ResolveAsync("04086"));
    }

    [Fact]
    public async Task LooksUpOtherZipsWithTheirTimeZone()
    {
        var http = StubHttp.Json(url => url.Contains("zippopotam")
            ? """{"places":[{"place name":"Boulder","longitude":"-105.2851","latitude":"40.0172","state abbreviation":"CO"}]}"""
            : """{"timezone":"America/Denver"}""");
        var resolver = new LocationResolver(http, new WeatherCache(), TestData.Options());

        var location = await resolver.ResolveAsync("80302");

        Assert.Equal("Boulder", location.Place);
        Assert.Equal("CO", location.State);
        Assert.Equal("America/Denver", location.TimeZoneId);
    }

    [Fact]
    public async Task UnknownZipThrowsNotFound()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var resolver = new LocationResolver(http, new WeatherCache(), TestData.Options());

        await Assert.ThrowsAsync<LocationNotFoundException>(() => resolver.ResolveAsync("00000"));
    }

    [Fact]
    public async Task UsesTheNearestActiveStationThatHasNormals()
    {
        // Closest first: no normals, highs only, closed (as Brunswick is), too new, then Durham, which qualifies.
        var http = StubHttp.Json(url => url.EndsWith("MultiStnData")
            ? """
            {"data":[
              {"meta":{"ll":[-69.96,43.93],"sids":["CLOSEST 2"],"name":"NO DATA","valid_daterange":[["1900-01-01","2026-10-29"],["1900-01-01","2026-10-29"],[],[]]},"data":["50","30","M","M"]},
              {"meta":{"ll":[-69.965,43.928],"sids":["HIGHS 2"],"name":"HIGHS ONLY","valid_daterange":[["1900-01-01","2026-10-29"],["1900-01-01","2026-10-29"],[],[]]},"data":["50","30","55","M"]},
              {"meta":{"ll":[-69.93333,43.9],"sids":["14611 1"],"name":"BRUNSWICK","valid_daterange":[["1945-02-01","2010-01-31"],["1945-02-01","2010-01-31"],[],[]]},"data":["M","M","53","36"]},
              {"meta":{"ll":[-69.86,43.93],"sids":["170409 2"],"name":"BATH","valid_daterange":[["2000-01-01","2026-10-29"],["2000-01-01","2026-10-29"],[],[]]},"data":["50","30","51","31"]},
              {"meta":{"ll":[-70.0963,43.9996],"sids":["172048 2"],"name":"DURHAM","valid_daterange":[["1994-09-01","2026-10-29"],["1994-09-01","2026-10-29"],[],[]]},"data":["50","30","50","30"]}
            ]}
            """
            : """{"data":[["2026-10-30","50","30"],["2026-10-31","49","M"]]}""");
        var cache = new WeatherCache();
        var options = TestData.Options();
        var normals = new AcisClimateNormals(http, cache, new ClimateStations(http, cache, options), options);

        var result = await normals.GetAsync(TestData.Topsham, new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31), default);

        Assert.Equal("DURHAM", result.Station);
        Assert.Equal(50, result.HighsF[new DateOnly(2026, 10, 30)]);
        Assert.Equal(30, result.LowsF[new DateOnly(2026, 10, 30)]);
        Assert.False(result.LowsF.ContainsKey(new DateOnly(2026, 10, 31)));

        // The search asks for both normals and each station's period of record.
        var search = await http.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("mint", search);
        Assert.Contains("valid_daterange", search);

        var normalsRequest = await http.Requests[1].Content!.ReadAsStringAsync();
        Assert.Contains("172048 2", normalsRequest);
        Assert.Contains("mint", normalsRequest);
    }
}
