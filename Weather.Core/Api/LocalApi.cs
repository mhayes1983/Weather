using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Weather.Core.Alerts;
using Weather.Core.DailyForecast;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Core.Api;

/// <summary>
/// The same API as Weather.Server's controllers, for a host without ASP.NET such as the
/// Android app. Its defaults, validation and status codes must stay matched to theirs.
/// </summary>
public sealed class LocalApi(
    ForecastTableService forecastTables,
    StationService stations,
    StationLookup lookup,
    AlertService alerts,
    DailyForecastService dailyForecasts,
    ILogger<LocalApi> logger)
{
    private const string OpenMeteoSource = "open-meteo";
    private const string DayPrefix = "/api/daily-forecast/";

    public async Task<LocalApiResponse> HandleAsync(
        string path, IReadOnlyDictionary<string, string?> query, CancellationToken cancellationToken = default)
    {
        try
        {
            string? value(string name) => query.GetValueOrDefault(name);

            return path switch
            {
                "/api/forecast" => await GetForecastAsync(value("zip"), cancellationToken),
                "/api/stations" => Ok(stations.Stations, LocalApiJson.Default.IReadOnlyListStationSummary),
                "/api/stations/lookup" => await LookUpAsync(value("q"), value("source")),
                "/api/station" => await GetStationAsync(value("id")),
                "/api/alerts" => await GetAlertsAsync(value("station")),
                "/api/daily-forecast" => await GetDailyForecastAsync(value("station")),
                _ when path.StartsWith(DayPrefix, StringComparison.Ordinal) => await GetDayAsync(path[DayPrefix.Length..], value("station")),
                _ => Problem(404, "Not found."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Local API request failed: {Error}", exception.GetType().Name);
            return Problem(500, "The request failed.");
        }
    }

    private async Task<LocalApiResponse> GetForecastAsync(string? zip, CancellationToken cancellationToken)
    {
        zip = string.IsNullOrWhiteSpace(zip) ? forecastTables.DefaultZip : zip.Trim();
        if (!LocationResolver.IsValidZip(zip))
        {
            return Problem(400, "Enter a 5-digit ZIP code.");
        }

        try
        {
            return Ok(await forecastTables.GetAsync(zip, cancellationToken), LocalApiJson.Default.ForecastTable);
        }
        catch (LocationNotFoundException exception)
        {
            return Problem(404, exception.Message);
        }
        catch (HttpRequestException)
        {
            return Problem(502, "The ZIP code lookup is unavailable right now.");
        }
    }

    private async Task<LocalApiResponse> LookUpAsync(string? q, string? source)
    {
        var query = q?.Trim() ?? "";
        if (source == OpenMeteoSource)
        {
            return await LookUpOpenMeteoAsync(query);
        }

        if (!StationLookup.IsValidQuery(query))
        {
            return Problem(400, "Enter a 5-digit ZIP code or a station ID.");
        }

        try
        {
            var match = await lookup.FindAsync(query);
            if (match is not null)
            {
                return Ok(match, LocalApiJson.Default.StationMatch);
            }

            return Problem(
                404,
                LocationResolver.IsValidZip(query)
                    ? "No station near that ZIP code has reported in the last hour."
                    : "No station with that ID has reported in the last hour. Station IDs are case sensitive.");
        }
        catch (StationLookupUnavailableException exception)
        {
            return Problem(503, exception.Message);
        }
    }

    private async Task<LocalApiResponse> LookUpOpenMeteoAsync(string zip)
    {
        if (!LocationResolver.IsValidZip(zip))
        {
            return Problem(400, "Enter a 5-digit ZIP code.");
        }

        try
        {
            var match = await lookup.FindOpenMeteoAsync(zip);
            return match is null
                ? Problem(404, "That ZIP code wasn't found.")
                : Ok(match, LocalApiJson.Default.StationMatch);
        }
        catch (StationLookupUnavailableException exception)
        {
            return Problem(503, exception.Message);
        }
    }

    private async Task<LocalApiResponse> GetStationAsync(string? id) =>
        await stations.GetReportAsync(Station(id)) is { } report
            ? Ok(report, LocalApiJson.Default.StationReport)
            : InvalidStation();

    private async Task<LocalApiResponse> GetAlertsAsync(string? station) =>
        await alerts.GetAsync(Station(station)) is { } report
            ? Ok(report, LocalApiJson.Default.AlertReport)
            : InvalidStation();

    private async Task<LocalApiResponse> GetDailyForecastAsync(string? station) =>
        await dailyForecasts.GetAsync(Station(station)) is { } report
            ? Ok(report, LocalApiJson.Default.DailyForecastReport)
            : InvalidStation();

    private async Task<LocalApiResponse> GetDayAsync(string date, string? station)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var day))
        {
            return Problem(400, "That isn't a valid date.");
        }

        try
        {
            return await dailyForecasts.GetDayAsync(day, Station(station)) is { } detail
                ? Ok(detail, LocalApiJson.Default.ForecastDayDetail)
                : Problem(404, "That day isn't available.");
        }
        catch (ForecastUnavailableException exception)
        {
            return Problem(503, exception.Message);
        }
    }

    private static string? Station(string? id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

    private static LocalApiResponse InvalidStation() => Problem(404, "That isn't a valid station ID.");

    private static LocalApiResponse Ok<T>(T value, JsonTypeInfo<T> type) => new(200, JsonSerializer.Serialize(value, type));

    private static LocalApiResponse Problem(int status, string detail) =>
        new(status, JsonSerializer.Serialize(new LocalApiProblem(status, detail), LocalApiJson.Default.LocalApiProblem));
}

public sealed record LocalApiResponse(int StatusCode, string Json);

public sealed record LocalApiProblem(int Status, string Detail);
