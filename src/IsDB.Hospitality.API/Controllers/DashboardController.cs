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

    [HttpGet("registration-type-stats")]
    public async Task<ActionResult<List<RegistrationTypeStatsDto>>> GetRegistrationTypeStats()
    {
        var result = await Mediator.Send(new GetRegistrationTypeStatsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Returns reception-focused summary data filtered by inbound flight ScheduledArrival date range.
    /// Used by the Reception Dashboard (Admin and Airport roles).
    /// </summary>
    [HttpGet("reception-summary")]
    public async Task<ActionResult<ReceptionSummaryDto>> GetReceptionSummary(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var f)
            ? f : DateTime.UtcNow.Date;
        var toDate = !string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var t)
            ? t : fromDate;
        var result = await Mediator.Send(new GetReceptionSummaryQuery(fromDate, toDate));
        return Ok(result);
    }
}
