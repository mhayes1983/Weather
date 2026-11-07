using Microsoft.AspNetCore.Mvc;
using Weather.Core.Models;
using Weather.Core.Snow;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/snow")]
public class SnowController(SnowTableService snowTables, SnowAlertService snowAlerts) : ControllerBase
{
    [HttpGet("towns")]
    public IReadOnlyList<SnowTownSummary> GetTowns() => snowTables.Towns;

    [HttpGet]
    public async Task<ActionResult<SnowTable>> Get([FromQuery] string? zips, [FromQuery] string? start, [FromQuery] string? end)
    {
        var (request, error) = snowTables.Parse(zips, start, end);
        return request is null
            ? Problem(error, statusCode: StatusCodes.Status400BadRequest)
            : await snowTables.GetAsync(request);
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<SnowAlertReport>> GetAlerts([FromQuery] string? zips)
    {
        var (request, error) = snowTables.Parse(zips, null, null);
        return request is null
            ? Problem(error, statusCode: StatusCodes.Status400BadRequest)
            : await snowAlerts.GetAsync(request.Zips);
    }
}
