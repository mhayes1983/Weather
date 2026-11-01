using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Core.Station;

public sealed class StationLookup(
    StationDirectory directory,
    StationService stations,
    IHttpClientFactory httpClientFactory,
    WeatherCache cache,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider,
    LocationResolver locations,
    ILogger<StationLookup> logger)
{
    private const string NearEndpoint = "https://api.weather.com/v3/location/near";
    private static readonly TimeSpan NearCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan RecentReport = TimeSpan.FromHours(1);

    public static bool IsValidQuery(string query) =>
        LocationResolver.IsValidZip(query) || StationDirectory.IsValidId(query);

    /// <summary>Returns null when nothing matches.</summary>
    /// <exception cref="StationLookupUnavailableException">Weather Underground couldn't be asked.</exception>
    public async Task<StationMatch?> FindAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(options.Value.WeatherUnderground.ApiKey))
        {
            throw new StationLookupUnavailableException("Add a Weather Underground key to look up stations.");
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            return LocationResolver.IsValidZip(query)
                ? await cache.GetOrCreateAsync<StationMatch?>(
                    $"station-near:{query}",
                    async () => (await FindNearAsync(query, timeout.Token), NearCacheDuration))
                : await FindByIdAsync(query, timeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Station lookup failed: {Error}", exception.GetType().Name);
            throw new StationLookupUnavailableException($"Station lookup failed. {SourceFailure.Describe(exception)}");
        }
    }

    /// <summary>Makes no Weather Underground calls, so it needs no key. Returns null for an unknown ZIP.</summary>
    /// <exception cref="StationLookupUnavailableException">The ZIP couldn't be checked.</exception>
    public async Task<StationMatch?> FindOpenMeteoAsync(string zip)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SourceTimeoutSeconds));
        try
        {
            var location = await locations.ResolveAsync(zip, timeout.Token);
            return new StationMatch(directory.CreateOpenMeteo(zip).Id, $"{location.Place}, {location.State}", null);
        }
        catch (LocationNotFoundException)
        {
            return null;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Open-Meteo lookup failed: {Error}", exception.GetType().Name);
            throw new StationLookupUnavailableException($"Station lookup failed. {SourceFailure.Describe(exception)}");
        }
    }

    private async Task<StationMatch?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        var station = directory.Find(id);
        if (station is null || id.Equals(AmbientWeatherStation.HomeId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var reading = await stations.GetCurrentAsync(station, cancellationToken);
            return new StationMatch(station.Id, reading.StationName ?? station.Id, null);
        }
        catch (StationNoDataException)
        {
            return null;
        }
    }

    private async Task<StationMatch?> FindNearAsync(string zip, CancellationToken cancellationToken)
    {
        var key = Uri.EscapeDataString(options.Value.WeatherUnderground.ApiKey!);
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        using var response = await client.GetAsync(
            $"{NearEndpoint}?postalKey={zip}:US&product=pws&format=json&apiKey={key}",
            cancellationToken);

        // An unrecognised ZIP is a failed lookup, not a service problem.
        if ((int)response.StatusCode is 400 or 404)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var location = document.RootElement.GetProperty("location");
        var ids = location.GetProperty("stationId");
        var names = location.GetProperty("stationName");
        var distances = location.GetProperty("distanceMi");
        var count = Math.Min(ids.GetArrayLength(), Math.Min(names.GetArrayLength(), distances.GetArrayLength()));

        // Results are nearest first. updateTimeUtc doesn't show when a station last
        // reported, so only its own latest reading can say whether it's usable.
        for (var i = 0; i < count; i++)
        {
            // A missing ID would otherwise mean the home station.
            if (ids[i].ValueKind == JsonValueKind.String
                && directory.Find(ids[i].GetString()) is { } station
                && await IsReportingAsync(station, cancellationToken))
            {
                var name = directory.IsConfigured(station.Id) ? station.Name : Text(names[i]) ?? station.Id;
                var distance = distances[i].ValueKind == JsonValueKind.Number ? distances[i].GetDouble() : (double?)null;
                return new StationMatch(station.Id, name, distance);
            }
        }

        return null;
    }

    // Some stations only send a reading or two, which leaves a nearly empty page.
    private async Task<bool> IsReportingAsync(IWeatherStation station, CancellationToken cancellationToken)
    {
        try
        {
            var reading = await stations.GetCurrentAsync(station, cancellationToken);
            return reading.TemperatureF is not null
                && timeProvider.GetUtcNow() - reading.ObservedAt <= RecentReport;
        }
        catch (Exception exception) when (IsThisStationsFault(exception))
        {
            return false;
        }
    }

    // Anything else, such as a bad key or the shared quota, would fail for every station too.
    private static bool IsThisStationsFault(Exception exception) => exception switch
    {
        StationNoDataException or JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or IndexOutOfRangeException => true,
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } => false,
        HttpRequestException { StatusCode: { } status } => (int)status is >= 400 and < 500,
        _ => false,
    };

    private static string? Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
