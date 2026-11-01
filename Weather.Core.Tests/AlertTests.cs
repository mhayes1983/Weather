using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Alerts;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Tests;

public class AlertTests
{
    private const string Id = "KMETEST1";

    private const string Alerts = """
        {"features":[
            {"properties":{
                "id":"advisory","event":"Frost Advisory","severity":"Minor",
                "headline":"Frost Advisory until morning","description":"Frost expected.","instruction":"Cover plants.",
                "onset":"2026-10-30T20:00:00-04:00","effective":"2026-10-30T10:00:00-04:00",
                "ends":"2026-10-31T09:00:00-04:00","expires":"2026-10-30T22:00:00-04:00",
                "areaDesc":"Private County"
            }},
            {"properties":{
                "id":"warning","event":"Winter Storm Warning","severity":"Severe",
                "headline":null,"description":"Heavy snow.","instruction":null,
                "onset":null,"effective":"2026-10-30T10:00:00-04:00",
                "ends":null,"expires":"2026-10-31T02:00:00-04:00"
            }},
            {"properties":{
                "id":"expired","event":"Wind Advisory","severity":"Moderate",
                "effective":"2026-10-29T10:00:00-04:00","ends":"2026-10-29T20:00:00-04:00"
            }},
            {"properties":{"id":"unknown","event":"Special Weather Statement"}}
        ]}
        """;

    private const string StationLocation = """
        {"observations":[{"obsTimeUtc":"2026-10-30T16:00:00Z","lat":42.3601,"lon":-71.0589,"neighborhood":"Private Road","imperial":{}}]}
        """;

    private static string Respond(string url) =>
        url.Contains("/alerts/active") ? Alerts : StationLocation;

    private static AlertService Service(StubHttp http, string? key = "secret-wu", WeatherCache? cache = null)
    {
        var options = TestData.Options(settings => settings.WeatherUnderground.ApiKey = key);
        var time = new FixedTime(TestData.Now);
        cache ??= new WeatherCache();
        return new AlertService(
            new StationDirectory([
                new AmbientWeatherStation(http, options, time),
                new WeatherUndergroundStation(new WeatherStationOptions { Id = Id, Name = "Test" }, http, options),
            ], http, options, TestData.Locations(http, options), TimeProvider.System),
            new StationLocator(cache),
            new NwsAlerts(http, time),
            cache,
            options,
            NullLogger<AlertService>.Instance);
    }

    [Fact]
    public async Task OrdersBySeverityAndDropsExpiredAlerts()
    {
        var report = (await Service(StubHttp.Json(Respond)).GetAsync())!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal(["warning", "advisory", "unknown"], report.Alerts.Select(alert => alert.Id));
    }

    [Fact]
    public async Task ReadsAlertDetails()
    {
        var alerts = (await Service(StubHttp.Json(Respond)).GetAsync())!.Alerts;

        var advisory = alerts[1];
        Assert.Equal("Frost Advisory", advisory.Event);
        Assert.Equal("Minor", advisory.Severity);
        Assert.Equal("Frost expected.", advisory.Description);
        Assert.Equal("Cover plants.", advisory.Instruction);
        Assert.Equal("2026-10-30T20:00:00-04:00", advisory.Starts);
        Assert.Equal("2026-10-31T09:00:00-04:00", advisory.Ends);
    }

    [Fact]
    public async Task OpenEndedAlertsHaveNoEndTime()
    {
        var warning = (await Service(StubHttp.Json(Respond)).GetAsync())!.Alerts[0];

        Assert.Equal("2026-10-30T10:00:00-04:00", warning.Starts);
        Assert.Null(warning.Ends);
    }

    [Fact]
    public async Task AlertsPastTheirMessageExpiryStayListed()
    {
        var http = StubHttp.Json(_ => """
            {"features":[{"properties":{"id":"flood","event":"Flood Warning","severity":"Severe","ends":null,"expires":"2026-10-30T01:00:00-04:00"}}]}
            """);

        var report = (await Service(http).GetAsync())!;

        Assert.Equal("flood", Assert.Single(report.Alerts).Id);
    }

    [Fact]
    public async Task MissingSeverityIsUnknown()
    {
        var alerts = (await Service(StubHttp.Json(Respond)).GetAsync())!.Alerts;

        Assert.Equal("Unknown", alerts[2].Severity);
    }

    [Fact]
    public async Task HomeUsesTheConfiguredLocationAndRequestsActualAlerts()
    {
        var http = StubHttp.Json(Respond);

        await Service(http).GetAsync();

        var query = Uri.UnescapeDataString(Assert.Single(http.Requests).RequestUri!.Query);
        Assert.Contains("point=43.9281,-69.9656", query);
        Assert.Contains("status=actual", query);
        Assert.Contains("message_type=alert,update", query);
    }

    [Fact]
    public async Task OtherStationsUseTheirCoordinates()
    {
        var http = StubHttp.Json(Respond);

        var report = (await Service(http).GetAsync(Id))!;

        Assert.Equal("Test", report.StationName);
        Assert.Contains(http.Requests, request => Uri.UnescapeDataString(request.RequestUri!.Query).Contains("point=42.3601,-71.0589"));
    }

    [Fact]
    public async Task RepeatRequestsUseTheCache()
    {
        var http = StubHttp.Json(Respond);
        var service = Service(http);

        await service.GetAsync();
        await service.GetAsync();

        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task LookedUpStationsWork()
    {
        var report = (await Service(StubHttp.Json(Respond)).GetAsync("KLOOKUP1"))!;

        Assert.Equal(SourceStatus.Ok, report.Status);
        Assert.Equal("KLOOKUP1", report.StationName);
    }

    [Fact]
    public async Task MalformedStationIsRejected()
    {
        var http = StubHttp.Json(Respond);

        Assert.Null(await Service(http).GetAsync("not valid!"));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task MissingKeyOnlyAffectsOtherStations()
    {
        var service = Service(StubHttp.Json(Respond), key: null);

        Assert.Equal(SourceStatus.NeedsKey, (await service.GetAsync(Id))!.Status);
        Assert.Equal(SourceStatus.Ok, (await service.GetAsync())!.Status);
    }

    [Fact]
    public async Task FailureIsReportedRatherThanShownAsNoAlerts()
    {
        var http = new StubHttp(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var report = (await Service(http).GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Equal("Source returned HTTP 503", report.Message);
        Assert.Empty(report.Alerts);
    }

    [Fact]
    public async Task FailureAfterSuccessDoesNotShowOldAlerts()
    {
        var failing = false;
        var http = new StubHttp(_ => failing
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Alerts) });
        var cache = new WeatherCache();
        var service = Service(http, cache: cache);

        Assert.NotEmpty((await service.GetAsync())!.Alerts);
        failing = true;
        cache.Remove("alerts:home");
        var report = (await service.GetAsync())!;

        Assert.Equal(SourceStatus.Error, report.Status);
        Assert.Empty(report.Alerts);
    }

    [Fact]
    public async Task ReportNeverContainsPrivateFields()
    {
        var json = JsonSerializer.Serialize(await Service(StubHttp.Json(Respond)).GetAsync(Id));

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("Private", json);
        Assert.DoesNotContain("42.36", json);
    }
}
