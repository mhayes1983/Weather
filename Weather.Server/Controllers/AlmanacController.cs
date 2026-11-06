using Microsoft.AspNetCore.Mvc;
using Weather.Core.Almanac;
using Weather.Core.Models;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/almanac")]
public class AlmanacController(AlmanacService almanac) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlmanacReport>> Get([FromQuery] string? station)
    {
        var report = await almanac.GetAsync(string.IsNullOrWhiteSpace(station) ? null : station.Trim());
        return report is null
            ? Problem("That isn't a valid station ID.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }
}
