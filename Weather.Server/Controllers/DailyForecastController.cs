using Microsoft.AspNetCore.Mvc;
using Weather.Core.DailyForecast;
using Weather.Core.Models;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/daily-forecast")]
public class DailyForecastController(DailyForecastService forecasts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DailyForecastReport>> Get([FromQuery] string? station)
    {
        var report = await forecasts.GetAsync(string.IsNullOrWhiteSpace(station) ? null : station.Trim());
        return report is null
            ? Problem("That station isn't configured.", statusCode: StatusCodes.Status404NotFound)
            : report;
    }

    [HttpGet("{date}")]
    public async Task<ActionResult<ForecastDayDetail>> GetDay(DateOnly date, [FromQuery] string? station)
    {
        var detail = await forecasts.GetDayAsync(date, string.IsNullOrWhiteSpace(station) ? null : station.Trim());
        return detail is null
            ? Problem("That day isn't available.", statusCode: StatusCodes.Status404NotFound)
            : detail;
    }
}
