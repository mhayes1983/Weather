using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Weather.Core.Api;
using Weather.Core.Models;
using Weather.Core.Station;
using Weather.Server.Controllers;

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

    private static readonly string History = $$"""[{"dateutc":{{NowMs}},"tempf":5}]""";

    /// <summary>Which accounts fail. Tests can change it between calls.</summary>
    private sealed class Outage
    {
        public bool First { get; set; }
        public bool Second { get; set; }
    }

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static StubHttp Http(Outage? outage = null) =>
        new(request =>
        {
            var url = request.RequestUri!.ToString();
            var second = url.Contains("apiKey=key-two");
            if (second ? outage?.Second == true : outage?.First == true)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            var body = url.Contains("zippopotam")
                ? """{"places":[{"place name":"Portland","longitude":"-70.2553","latitude":"43.6615","state abbreviation":"ME"}]}"""
                : url.Contains("open-meteo") ? """{"timezone":"America/New_York"}"""
                : url.Contains("/devices/") ? History
                : second ? SecondAccount : FirstAccount;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

    private static AmbientCatalog Catalog(StubHttp http, IOptions<WeatherOptions>? options = null, TimeProvider? time = null) =>
        TestData.Ambient(http, options ?? Options(), time);

    private static async Task<string> IdOf(string name) =>
        (await Catalog(Http()).GetStationsAsync()).Single(station => station.Name == name).Id;

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
        var stations = await Catalog(Http(new Outage { Second = true })).GetStationsAsync();

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

    private static ServiceProvider Provider(StubHttp http) =>
        new ServiceCollection()
            .AddLogging()
            .AddWeatherCore(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AmbientWeather:ApiKey"] = "key-one",
                    ["AmbientWeather:ApplicationKey"] = "app-one",
                    ["AmbientWeather:Accounts:0:ApiKey"] = "key-two",
                    ["AmbientWeather:Accounts:0:ApplicationKey"] = "app-two",
                    ["AmbientWeather:RequestSpacingMilliseconds"] = "0",
                    ["DefaultLocation:Zip"] = "04086",
                })
                .Build())
            .AddSingleton<IHttpClientFactory>(http)
            .BuildServiceProvider();

    [Fact]
    public async Task AnUnreadAccountMakesTheListIncompleteRatherThanShorter()
    {
        var outage = new Outage { Second = true };
        using var provider = Provider(Http(outage));

        var list = await provider.GetRequiredService<AmbientCatalog>().ListStationsAsync();
        Assert.False(list.Complete);
        await Assert.ThrowsAsync<StationListUnavailableException>(() => provider.GetRequiredService<StationDirectory>().GetStationsAsync());

        var local = await provider.GetRequiredService<LocalApi>().HandleAsync("/api/stations", new Dictionary<string, string?>());
        Assert.Equal(503, local.StatusCode);

        var controller = new StationController(provider.GetRequiredService<StationService>(), provider.GetRequiredService<StationLookup>());
        var result = Assert.IsType<ObjectResult>((await controller.List(default)).Result);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task AStationOnAnUnreadAccountIsStillFoundAndRecovers()
    {
        var campId = await IdOf("Camp");
        var outage = new Outage { Second = true };
        using var provider = Provider(Http(outage));
        var service = provider.GetRequiredService<StationService>();

        // Not a 404, which would tell a browser its saved station is gone.
        var report = await service.GetReportAsync(campId);
        Assert.NotNull(report);
        Assert.Equal(StationStatus.Error, report.Status);

        outage.Second = false;
        var camp = await provider.GetRequiredService<StationDirectory>().FindAsync(campId);
        Assert.Equal(3, (await camp!.GetCurrentAsync(default)).TemperatureF);
    }

    [Fact]
    public async Task UnknownOrMadeUpIdsAreNotFound()
    {
        var complete = Catalog(Http());
        Assert.Null(await complete.FindAsync("amb-000000000000"));

        var incomplete = Catalog(Http(new Outage { Second = true }));
        Assert.Null(await incomplete.FindAsync("amb-not-a-real-id"));
        Assert.NotNull(await incomplete.FindAsync("amb-000000000000"));
    }

    [Fact]
    public async Task ALaterFailureKeepsTheLastGoodList()
    {
        var outage = new Outage();
        var time = new MovableTime(TestData.Now);
        var catalog = Catalog(Http(outage), time: time);
        Assert.Equal(["Barn", "Camp"], (await catalog.GetStationsAsync()).Select(station => station.Name));

        outage.Second = true;
        time.Now += TimeSpan.FromHours(2);
        var list = await catalog.ListStationsAsync();

        Assert.True(list.Complete);
        Assert.Equal(["Barn", "Camp"], list.Stations.Select(station => station.Name));
    }

    [Fact]
    public async Task HomeOnALaterAccountSurvivesTheFirstAccountFailing()
    {
        var options = Options(settings => settings.Stations = [new AmbientStationOptions { MacAddress = Camp, Home = true }]);
        var catalog = Catalog(Http(new Outage { First = true }), options);

        Assert.Equal(3, (await catalog.Home.GetCurrentAsync(default)).TemperatureF);
    }

    [Fact]
    public async Task HomeStillFailsWhenItsAccountFails()
    {
        var catalog = Catalog(Http(new Outage { First = true }));

        await Assert.ThrowsAsync<HttpRequestException>(() => catalog.Home.GetCurrentAsync(default));
    }

    [Fact]
    public async Task OpenMeteoHomeListsEveryAmbientDevice()
    {
        var options = Options();
        options.Value.HomeStationSource = HomeStationSource.OpenMeteo;

        var stations = await Catalog(Http(), options).GetStationsAsync();

        Assert.Equal(["Backyard", "Barn", "Camp"], stations.Select(station => station.Name));
    }

    [Fact]
    public async Task UnnamedHomeIsHeadedByItsAmbientNameButNamedHomeIsNot()
    {
        Assert.Equal("Backyard", (await Catalog(Http()).Home.GetCurrentAsync(default)).StationName);

        var named = Catalog(Http(), Options(settings => settings.Name = "House"));
        Assert.Equal("House", named.Home.Name);
        Assert.Equal("House", (await named.Home.GetCurrentAsync(default)).StationName);
    }

    [Fact]
    public async Task OtherDevicesReadTheirOwnHistory()
    {
        var http = Http();
        var barn = (await Catalog(http).GetStationsAsync()).Single(station => station.Name == "Barn");

        var history = await barn.GetHistoryAsync(default);

        Assert.Equal(5, Assert.Single(history).TemperatureF);
        Assert.Contains(http.Requests, request =>
            request.RequestUri!.ToString().Contains($"/devices/{Uri.EscapeDataString(Barn)}?apiKey=key-one"));
    }

    [Fact]
    public async Task OtherDevicesAreLocatedByTheirZip()
    {
        var options = Options(settings => settings.Stations = [new AmbientStationOptions { MacAddress = Camp, Zip = "04101" }]);
        var camp = (await Catalog(Http(), options).GetStationsAsync()).Single(station => station.Name == "Camp");

        var point = await camp.GetCoordinatesAsync(default);

        Assert.Equal(43.6615, point.Latitude);
        Assert.Equal(-70.2553, point.Longitude);
    }

    [Fact]
    public async Task SetupListingSharesTheSavedAccount()
    {
        var options = Options();
        var catalog = Catalog(Http(), options);
        var check = new KeyCheck(Http(), options, catalog);

        Assert.Same(catalog.Accounts[0], catalog.FindAccount(" key-one ", "app-one"));
        Assert.Null(catalog.FindAccount("key-one", "other"));
        Assert.Equal(2, (await check.ListAmbientDevicesAsync("key-one", "app-one"))!.Count);
    }
}
