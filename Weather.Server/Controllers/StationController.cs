using Microsoft.AspNetCore.Mvc;
using Weather.Core.Models;
using Weather.Core.Station;

namespace Weather.Server.Controllers;

[ApiController]
[Route("api/station")]
public class StationController(StationService station) : ControllerBase
{
    [HttpGet]
    public Task<StationReport> Get() => station.GetReportAsync();
}
