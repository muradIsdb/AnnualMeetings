using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Application.Features.Guests.Commands;
using IsDB.Hospitality.Application.Features.Guests.Queries;
using IsDB.Hospitality.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class GuestsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GuestSummaryDto>>> GetGuests(
        [FromQuery] GuestStatus? status = null,
        [FromQuery] bool? isCritical = null)
    {
        var result = await Mediator.Send(new GetGuestsQuery(status, isCritical));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuestDto>> GetGuest(Guid id)
    {
        var result = await Mediator.Send(new GetGuestByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var success = await Mediator.Send(new UpdateGuestStatusCommand(id, request.Status, request.Notes));
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/checklist/{checklistItemId:guid}")]
    public async Task<IActionResult> CompleteChecklistItem(Guid id, Guid checklistItemId, [FromBody] CompleteChecklistRequest? request = null)
    {
        var success = await Mediator.Send(new CompleteChecklistItemCommand(id, checklistItemId, CurrentUserId, request?.Notes));
        if (!success) return NotFound();
        return NoContent();
    }
}

public record UpdateStatusRequest(GuestStatus Status, string? Notes = null);
public record CompleteChecklistRequest(string? Notes = null);
