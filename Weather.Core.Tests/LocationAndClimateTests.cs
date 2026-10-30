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
    public async Task UsesNearestStationThatHasNormals()
    {
        var http = StubHttp.Json(url => url.EndsWith("MultiStnData")
            ? """
            {"data":[
              {"meta":{"ll":[-69.96,43.93],"sids":["CLOSEST 2"],"name":"NO DATA"},"data":["M"]},
              {"meta":{"ll":[-69.93333,43.9],"sids":["14611 1"],"name":"BRUNSWICK"},"data":["53"]},
              {"meta":{"ll":[-70.30443,43.64244],"sids":["14764 1"],"name":"PORTLAND JETPORT"},"data":["54"]}
            ]}
            """
            : """{"data":[["2026-10-30","53"],["2026-10-31","52"]]}""");
        var normals = new AcisClimateNormals(http, new WeatherCache());

        var result = await normals.GetAsync(TestData.Topsham, new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 31), default);

        Assert.Equal("BRUNSWICK", result.Station);
        Assert.Equal(53, result.HighsF[new DateOnly(2026, 10, 30)]);
        Assert.Contains("14611 1", await http.Requests[1].Content!.ReadAsStringAsync());
    }
}
