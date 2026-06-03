using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.Features.Dashboard.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    public DashboardController(IAppDbContext db) { _db = db; }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetDashboardSummaryQuery(activeEventCode));
        return Ok(result);
    }

    [HttpGet("registration-type-stats")]
    public async Task<ActionResult<List<RegistrationTypeStatsDto>>> GetRegistrationTypeStats(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetRegistrationTypeStatsQuery(activeEventCode));
        return Ok(result);
    }

    /// <summary>
    /// Returns hotel-focused summary data: occupancy, en-route guests, departure status, recent check-ins.
    /// Used by the Hotel Dashboard (Admin and Hotel roles).
    /// </summary>
    [HttpGet("hotel-summary")]
    public async Task<ActionResult<HotelSummaryDto>> GetHotelSummary(CancellationToken ct)
    {
        var activeEventCode = (await _db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetHotelSummaryQuery(activeEventCode));
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
