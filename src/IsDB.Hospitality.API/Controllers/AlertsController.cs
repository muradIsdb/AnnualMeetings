using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Application.Features.Alerts.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class AlertsController : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateAlert([FromBody] CreateAlertDto request)
    {
        var id = await Mediator.Send(new CreateAlertCommand(
            request.GuestId, request.Title, request.Message, request.Severity));
        return CreatedAtAction(nameof(CreateAlert), new { id }, new { id });
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveAlert(Guid id, [FromBody] ResolveAlertDto? request = null)
    {
        var success = await Mediator.Send(new ResolveAlertCommand(id, CurrentUserId, request?.ResolutionNotes));
        if (!success) return NotFound();
        return NoContent();
    }
}
