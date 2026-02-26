using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.Features.Dashboard.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var result = await Mediator.Send(new GetDashboardSummaryQuery());
        return Ok(result);
    }
}
