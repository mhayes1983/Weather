using Microsoft.AspNetCore.Mvc;
using Weather.Core;
using Weather.Core.Locations;
using Weather.Core.Models;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/forecast")]
public class ForecastController(ForecastTableService forecastTables) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ForecastTable>> Get([FromQuery] string? zip, CancellationToken cancellationToken)
    {
        zip = string.IsNullOrWhiteSpace(zip) ? forecastTables.DefaultZip : zip.Trim();
        if (!LocationResolver.IsValidZip(zip))
        {
            return Problem("Enter a 5-digit ZIP code.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return await forecastTables.GetAsync(zip, cancellationToken);
        }
        catch (LocationNotFoundException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (HttpRequestException)
        {
            return Problem("The ZIP code lookup is unavailable right now.", statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
