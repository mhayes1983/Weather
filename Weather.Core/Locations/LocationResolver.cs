using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Weather.Core.Models;

namespace Weather.Core.Locations;

public sealed partial class LocationResolver(
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);

    public static bool IsValidZip(string zip) => ZipPattern().IsMatch(zip);

    public Task<ForecastLocation> ResolveAsync(string zip, CancellationToken cancellationToken = default)
    {
        var defaultLocation = options.Value.DefaultLocation;
        if (zip == defaultLocation.Zip)
        {
            return Task.FromResult(defaultLocation);
        }

        return cache.GetOrCreateAsync(
            $"location:{zip}",
            async () => (await LookUpAsync(zip, cancellationToken), CacheDuration));
    }

    private async Task<ForecastLocation> LookUpAsync(string zip, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        using var response = await client.GetAsync($"https://api.zippopotam.us/us/{zip}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new LocationNotFoundException(zip);
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var place = document.RootElement.GetProperty("places")[0];
        var latitude = double.Parse(place.GetProperty("latitude").GetString()!, CultureInfo.InvariantCulture);
        var longitude = double.Parse(place.GetProperty("longitude").GetString()!, CultureInfo.InvariantCulture);

        return new ForecastLocation
        {
            Zip = zip,
            Place = place.GetProperty("place name").GetString()!,
            State = place.GetProperty("state abbreviation").GetString()!,
            Latitude = latitude,
            Longitude = longitude,
            TimeZoneId = await GetTimeZoneAsync(client, latitude, longitude, cancellationToken),
        };
    }

    private static async Task<string> GetTimeZoneAsync(
        HttpClient client, double latitude, double longitude, CancellationToken cancellationToken)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&daily=temperature_2m_max&timezone=auto&forecast_days=1");

        using var document = JsonDocument.Parse(await client.GetStringAsync(url, cancellationToken));
        return document.RootElement.GetProperty("timezone").GetString()!;
    }

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex ZipPattern();
}
