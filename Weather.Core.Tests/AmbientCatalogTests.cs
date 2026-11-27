using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class AmbientCatalogTests
{
    private const string Yard = "AA:BB:CC:DD:EE:01";
    private const string Barn = "AA:BB:CC:DD:EE:02";
    private const string Camp = "AA:BB:CC:DD:EE:03";

    private static readonly long NowMs = TestData.Now.ToUnixTimeMilliseconds();

    private static string Device(string mac, string? name, double tempf)
    {
        var info = name is null ? "{}" : $$"""{"name":"{{name}}"}""";
        return $$$"""{"macAddress":"{{{mac}}}","info":{{{info}}},"lastData":{"dateutc":{{{NowMs}}},"tempf":{{{tempf}}}}}""";
    }

    private static readonly string FirstAccount = $"[{Device(Yard, "Backyard", 1)},{Device(Barn, "Barn", 2)}]";
    private static readonly string SecondAccount = $"[{Device(Camp, "Camp", 3)}]";

    private static IOptions<WeatherOptions> Options(Action<AmbientWeatherOptions>? configure = null) => TestData.Options(options =>
    {
        options.AmbientWeather.ApiKey = "key-one";
        options.AmbientWeather.ApplicationKey = "app-one";
        options.AmbientWeather.Accounts = [new AmbientAccountOptions { ApiKey = "key-two", ApplicationKey = "app-two" }];
        options.AmbientWeather.RequestSpacingMilliseconds = 0;
        configure?.Invoke(options.AmbientWeather);
    });

    private static StubHttp Http(bool secondFails = false) =>
        new(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("apiKey=key-two") && secondFails)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(url.Contains("apiKey=key-two") ? SecondAccount : FirstAccount),
            };
        });

    private static AmbientCatalog Catalog(StubHttp http, IOptions<WeatherOptions>? options = null) =>
        TestData.Ambient(http, options ?? Options());

    [Fact]
    public async Task ListsEveryDeviceOnEveryAccountExceptHome()
    {
        var catalog = Catalog(Http());

        var stations = await catalog.GetStationsAsync();

        Assert.Equal(["Barn", "Camp"], stations.Select(station => station.Name));
        Assert.All(stations, station => Assert.Matches("^amb-[0-9a-f]{12}$", station.Id));
        Assert.Equal(1, (await catalog.Home.GetCurrentAsync(default)).TemperatureF);
        Assert.Equal("Home station", catalog.Home.Name);
    }

    [Fact]
    public async Task IdsAreStableAndDontRevealTheMacAddress()
    {
        var first = await Catalog(Http()).GetStationsAsync();
        var second = await Catalog(Http()).GetStationsAsync();

        Assert.Equal(first.Select(station => station.Id), second.Select(station => station.Id));
        Assert.All(first, station => Assert.DoesNotContain("aabbccddee", station.Id, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SettingsNameLocateOrderAndHideDevices()
    {
        var options = Options(settings => settings.Stations =
        [
            new AmbientStationOptions { MacAddress = Camp.ToLowerInvariant(), Name = "Camp station", Zip = "04102" },
            new AmbientStationOptions { MacAddress = Barn, Hidden = true },
        ]);

        var stations = await Catalog(Http(), options).GetStationsAsync();

        var camp = Assert.Single(stations);
        Assert.Equal("Camp station", camp.Name);
        Assert.Equal("04102", camp.Zip);
        Assert.True(camp.CanLocate);
    }

    [Fact]
    public async Task SettingsCanMatchByStationId()
    {
        var barnId = (await Catalog(Http()).GetStationsAsync()).Single(station => station.Name == "Barn").Id;
        var options = Options(settings => settings.Stations = [new AmbientStationOptions { DeviceId = barnId, Name = "Hay barn" }]);

        var stations = await Catalog(Http(), options).GetStationsAsync();

        Assert.Equal(["Hay barn", "Camp"], stations.Select(station => station.Name));
    }

    [Fact]
    public async Task AnyDeviceCanBeHome()
    {
        var options = Options(settings => settings.Stations =
            [new AmbientStationOptions { MacAddress = Camp, Home = true, Name = "Camp", Zip = "04102" }]);
        var catalog = Catalog(Http(), options);

        Assert.Equal("Camp", catalog.Home.Name);
        Assert.Equal("04102", catalog.Home.Zip);
        Assert.Equal(3, (await catalog.Home.GetCurrentAsync(default)).TemperatureF);
        Assert.Equal(["Backyard", "Barn"], (await catalog.GetStationsAsync()).Select(station => station.Name));
    }

    [Fact]
    public async Task LegacyMacAddressStillPicksHome()
    {
        var catalog = Catalog(Http(), Options(settings => settings.MacAddress = Barn));

        Assert.Equal(2, (await catalog.Home.GetCurrentAsync(default)).TemperatureF);
        Assert.Equal(["Backyard", "Camp"], (await catalog.GetStationsAsync()).Select(station => station.Name));
    }

    [Fact]
    public async Task AFailedAccountDropsOnlyItsOwnStations()
    {
        var stations = await Catalog(Http(secondFails: true)).GetStationsAsync();

        Assert.Equal(["Barn"], stations.Select(station => station.Name));
    }

    [Fact]
    public async Task OneDeviceListServesEveryStationOnTheAccount()
    {
        var http = Http();
        var catalog = Catalog(http);
        var barn = (await catalog.GetStationsAsync()).Single(station => station.Name == "Barn");

        Assert.Equal(1, (await catalog.Home.GetCurrentAsync(default)).TemperatureF);
        var reading = await barn.GetCurrentAsync(default);
        Assert.Equal(2, reading.TemperatureF);
        Assert.Equal("Barn", reading.StationName);
        Assert.Single(http.Requests, request => request.RequestUri!.ToString().Contains("apiKey=key-one"));
    }

    [Fact]
    public async Task OtherStationsWithoutAZipCantBeLocated()
    {
        var barn = (await Catalog(Http()).GetStationsAsync()).Single(station => station.Name == "Barn");

        Assert.False(barn.CanLocate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => barn.GetCoordinatesAsync(default));
    }

    [Fact]
    public async Task UnnamedDevicesAreNumbered()
    {
        var http = StubHttp.Json(_ => $"[{Device(Yard, null, 1)},{Device(Barn, null, 2)}]");
        var options = TestData.Options(options =>
        {
            options.AmbientWeather.ApiKey = "key-one";
            options.AmbientWeather.ApplicationKey = "app-one";
        });

        var stations = await Catalog(http, options).GetStationsAsync();

        Assert.Equal("Ambient station 2", Assert.Single(stations).Name);
    }

    [Fact]
    public async Task NoKeysMeansNoOtherStationsAndNoCalls()
    {
        var http = Http();
        var catalog = Catalog(http, TestData.Options());

        Assert.Empty(await catalog.GetStationsAsync());
        Assert.False(catalog.Home.HasKeys);
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task DirectoryListsDevicesAfterHomeAndFindsThem()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AmbientWeather:ApiKey"] = "key-one",
                ["AmbientWeather:ApplicationKey"] = "app-one",
                ["AmbientWeather:RequestSpacingMilliseconds"] = "0",
                ["Stations:0:Id"] = "KFIRST1",
                ["DefaultLocation:Zip"] = "04086",
            })
            .Build();
        var http = Http();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddWeatherCore(configuration)
            .AddSingleton<IHttpClientFactory>(http)
            .BuildServiceProvider();
        var directory = provider.GetRequiredService<StationDirectory>();

        var stations = await directory.GetStationsAsync();

        Assert.Equal(["Home station", "Barn", "KFIRST1"], stations.Select(station => station.Name));
        Assert.Equal("Barn", (await directory.FindAsync(stations[1].Id.ToUpperInvariant()))!.Name);
        Assert.Null(await directory.FindAsync("amb-000000000000"));
    }
}
