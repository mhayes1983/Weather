using System.Net;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Tests;

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class StubHttp(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler, IHttpClientFactory
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttp Json(Func<string, string> bodyForUrl) =>
        new(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(bodyForUrl(request.RequestUri!.ToString())),
        });

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}

internal static class TestData
{
    public static readonly ForecastLocation Topsham = new()
    {
        Zip = "04086",
        Place = "Topsham",
        State = "ME",
        Latitude = 43.9281,
        Longitude = -69.9656,
        TimeZoneId = "America/New_York",
    };

    public static readonly DateTimeOffset Now = new(2026, 10, 30, 16, 0, 0, TimeSpan.Zero);

    public static LocationResolver Locations(IHttpClientFactory http, IOptions<WeatherOptions> options) =>
        new(http, new WeatherCache(), options);

    public static IOptions<WeatherOptions> Options(Action<WeatherOptions>? configure = null)
    {
        var options = new WeatherOptions { DefaultLocation = Topsham };
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }
}
