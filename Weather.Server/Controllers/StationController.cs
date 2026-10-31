using Microsoft.AspNetCore.Mvc;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api")]
public class StationController(StationService stations, StationLookup lookup) : ControllerBase
{
    [HttpGet("stations")]
    public IReadOnlyList<StationSummary> List() => stations.Stations;

    [HttpGet("stations/lookup")]
    public async Task<ActionResult<StationMatch>> LookUp([FromQuery] string? q)
    {
        var query = q?.Trim() ?? "";
        if (!StationLookup.IsValidQuery(query))
        {
            return Problem("Enter a 5-digit ZIP code or a station ID.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var match = await lookup.FindAsync(query);
            if (match is not null)
            {
                return match;
            }

            return Problem(
                LocationResolver.IsValidZip(query)
                    ? "No station near that ZIP code has reported in the last hour."
                    : "No station with that ID has reported in the last hour. Station IDs are case sensitive.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (StationLookupUnavailableException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("station")]
    public async Task<ActionResult<StationReport>> Get([FromQuery] string? id)
    {
        var report = await stations.GetReportAsync(string.IsNullOrWhiteSpace(id) ? null : id.Trim());
        return report is null
            ? Problem("That isn't a valid station ID.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }
}
