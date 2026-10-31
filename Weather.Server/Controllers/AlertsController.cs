using Microsoft.AspNetCore.Mvc;
using Weather.Core.Alerts;
using Weather.Core.Models;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController(AlertService alerts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AlertReport>> Get([FromQuery] string? station)
    {
        var report = await alerts.GetAsync(string.IsNullOrWhiteSpace(station) ? null : station.Trim());
        return report is null
            ? Problem("That isn't a valid station ID.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }
}
