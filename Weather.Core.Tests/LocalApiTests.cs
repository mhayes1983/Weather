using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather.Core.Alerts;
using Weather.Core.Api;
using Weather.Core.DailyForecast;
using Weather.Core.Models;
using Weather.Core.Station;
using Weather.Server.Controllers;

namespace Weather.Core.Tests;

/// <summary>The app's API must answer exactly as the site's controllers do.</summary>
public class LocalApiTests
{
    // What Program.cs gives the controllers.
    private static readonly JsonSerializerOptions ServerJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string Zip = "04086";

    private static string OpenMeteoCurrent() => $$"""
        {
          "current": { "time": {{TestData.Now.ToUnixTimeSeconds()}}, "interval": 900, "temperature_2m": 40 },
          "hourly": { "time": [{{TestData.Now.ToUnixTimeSeconds()}}], "temperature_2m": [40] }
        }
        """;

    private static ServiceProvider Services(string body, Dictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["DefaultLocation:Zip"] = Zip,
            ["DefaultLocation:Place"] = "Topsham",
            ["DefaultLocation:State"] = "ME",
            ["DefaultLocation:Latitude"] = "43.9281",
            ["DefaultLocation:Longitude"] = "-69.9656",
            ["DefaultLocation:TimeZoneId"] = "America/New_York",
        };

        foreach (var (key, value) in settings ?? [])
        {
            values[key] = value;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTime(TestData.Now));
        services.AddWeatherCore(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddSingleton<IHttpClientFactory>(StubHttp.Json(_ => body));
        return services.BuildServiceProvider();
    }

    private static Task<LocalApiResponse> Ask(ServiceProvider services, string path, params (string Key, string? Value)[] query) =>
        services.GetRequiredService<LocalApi>().HandleAsync(path, query.ToDictionary(item => item.Key, item => item.Value));

    private static void AssertSame<T>(ActionResult<T> result, LocalApiResponse response)
    {
        if (result.Result is ObjectResult { Value: ProblemDetails problem } objectResult)
        {
            Assert.Equal(objectResult.StatusCode, response.StatusCode);
            using var document = JsonDocument.Parse(response.Json);
            Assert.Equal(problem.Detail, document.RootElement.GetProperty("detail").GetString());
            return;
        }

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(result.Value, ServerJson), response.Json);
    }

    private static StationController Stations(ServiceProvider services) =>
        new(services.GetRequiredService<StationService>(), services.GetRequiredService<StationLookup>());

    [Theory]
    [InlineData("home")]
    [InlineData("om-04086")]
    [InlineData("not/valid")]
    public async Task StationMatchesTheController(string id)
    {
        using var services = Services(OpenMeteoCurrent());

        AssertSame(await Stations(services).Get(id), await Ask(services, "/api/station", ("id", id)));
    }

    [Theory]
    [InlineData("04086", "open-meteo")]
    [InlineData("ab", "open-meteo")]
    [InlineData("ab", null)]
    [InlineData("04086", null)]
    public async Task LookupMatchesTheController(string query, string? source)
    {
        using var services = Services("{}");

        AssertSame(await Stations(services).LookUp(query, source), await Ask(services, "/api/stations/lookup", ("q", query), ("source", source)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData(" ")]
    public async Task ForecastMatchesTheController(string zip)
    {
        using var services = Services("{}");
        var controller = new ForecastController(services.GetRequiredService<ForecastTableService>());

        var result = await controller.Get(zip, CancellationToken.None);
        var response = await Ask(services, "/api/forecast", ("zip", zip));

        if (result.Result is null)
        {
            // The two tables are built separately, so their generated times differ.
            using var expected = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, ServerJson));
            using var actual = JsonDocument.Parse(response.Json);
            Assert.Equal(
                expected.RootElement.GetProperty("columns").GetRawText(),
                actual.RootElement.GetProperty("columns").GetRawText());
        }
        else
        {
            AssertSame(result, response);
        }
    }

    [Theory]
    [InlineData("om-04086")]
    [InlineData("not/valid")]
    public async Task AlertsMatchTheController(string station)
    {
        using var services = Services("""{"features":[]}""");
        var controller = new AlertsController(services.GetRequiredService<AlertService>());

        AssertSame(await controller.Get(station), await Ask(services, "/api/alerts", ("station", station)));
    }

    [Fact]
    public async Task DailyForecastMatchesTheController()
    {
        using var services = Services("{}");
        var controller = new DailyForecastController(services.GetRequiredService<DailyForecastService>());

        AssertSame(await controller.Get("not/valid"), await Ask(services, "/api/daily-forecast", ("station", "not/valid")));
    }

    [Fact]
    public async Task EnumsAreStringsAndNamesAreCamelCase()
    {
        using var services = Services(OpenMeteoCurrent());

        var response = await Ask(services, "/api/station", ("id", "om-04086"));

        using var document = JsonDocument.Parse(response.Json);
        Assert.Equal("Ok", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Last24Hours", document.RootElement.GetProperty("historyRange").GetString());
    }

    [Fact]
    public async Task UnknownPathIs404()
    {
        using var services = Services("{}");

        Assert.Equal(404, (await Ask(services, "/api/nothing")).StatusCode);
    }

    [Fact]
    public async Task BadDateIs400()
    {
        using var services = Services("{}");

        Assert.Equal(400, (await Ask(services, "/api/daily-forecast/soon")).StatusCode);
    }

    [Fact]
    public async Task OpenMeteoHomeStationNeedsNoKeysAndKeepsTheHomeId()
    {
        using var services = Services(OpenMeteoCurrent(), new() { ["HomeStationSource"] = "OpenMeteo" });
        var stations = services.GetRequiredService<StationService>();

        var report = await stations.GetReportAsync();

        Assert.Equal(StationStatus.Ok, report!.Status);
        Assert.True(report.Modelled);
        Assert.Equal(AmbientWeatherStation.HomeId, stations.Stations[0].Id);
        Assert.Equal("ZIP 04086", stations.Stations[0].Name);
    }

    [Fact]
    public async Task AmbientStaysTheDefaultHomeStation()
    {
        using var services = Services("{}");

        var report = await services.GetRequiredService<StationService>().GetReportAsync();

        Assert.Equal(StationStatus.NeedsKey, report!.Status);
        Assert.False(report.Modelled);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, KeyCheckResult.Valid)]
    [InlineData(HttpStatusCode.NoContent, KeyCheckResult.Valid)]
    [InlineData(HttpStatusCode.Unauthorized, KeyCheckResult.Invalid)]
    [InlineData(HttpStatusCode.Forbidden, KeyCheckResult.Invalid)]
    [InlineData(HttpStatusCode.TooManyRequests, KeyCheckResult.CouldNotCheck)]
    [InlineData(HttpStatusCode.InternalServerError, KeyCheckResult.CouldNotCheck)]
    public async Task KeyChecksReadTheStatus(HttpStatusCode status, KeyCheckResult expected)
    {
        var http = new StubHttp(_ => new HttpResponseMessage(status));
        var check = new KeyCheck(http, TestData.Options());

        Assert.Equal(expected, await check.CheckWeatherUndergroundAsync("a key"));
        Assert.Equal(expected, await check.CheckAmbientWeatherAsync("a key", "another"));
        Assert.Contains("apiKey=a%20key", http.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task KeyCheckCouldNotCheckWhenTheNetworkFails()
    {
        var http = new StubHttp(_ => throw new HttpRequestException());

        Assert.Equal(KeyCheckResult.CouldNotCheck, await new KeyCheck(http, TestData.Options()).CheckWeatherUndergroundAsync("key"));
    }
}
