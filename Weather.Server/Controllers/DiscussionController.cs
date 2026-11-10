using Microsoft.AspNetCore.Mvc;
using Weather.Core.DailyForecast;
using Weather.Core.Models;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/discussion")]
public class DiscussionController(DailyForecastService forecasts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DiscussionReport>> Get([FromQuery] string? station)
    {
        var report = await forecasts.GetDiscussionAsync(string.IsNullOrWhiteSpace(station) ? null : station.Trim());
        return report is null
            ? Problem("That isn't a valid station ID.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }
}
