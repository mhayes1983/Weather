using Microsoft.AspNetCore.Mvc;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api")]
public class StationController(StationService stations) : ControllerBase
{
    [HttpGet("stations")]
    public IReadOnlyList<StationSummary> List() => stations.Stations;

    [HttpGet("station")]
    public async Task<ActionResult<StationReport>> Get([FromQuery] string? id)
    {
        var report = await stations.GetReportAsync(string.IsNullOrWhiteSpace(id) ? null : id.Trim());
        return report is null
            ? Problem("That station isn't configured.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }
}
