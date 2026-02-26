using IsDB.Hospitality.Application.DTOs.Vehicles;
using IsDB.Hospitality.Application.Features.Vehicles.Commands;
using IsDB.Hospitality.Application.Features.Vehicles.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class VehiclesController : ApiControllerBase
{
    [HttpGet("available")]
    public async Task<ActionResult<List<VehicleDto>>> GetAvailable()
    {
        var result = await Mediator.Send(new GetAvailableVehiclesQuery());
        return Ok(result);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignVehicle([FromBody] AssignVehicleDto request)
    {
        var success = await Mediator.Send(new AssignVehicleCommand(
            request.GuestId, request.VehicleId, CurrentUserId, request.Notes, request.EstimatedArrivalTime));
        if (!success) return BadRequest(new { message = "Vehicle is not available or guest not found." });
        return NoContent();
    }

    [HttpPost("assign-by-barcode")]
    public async Task<IActionResult> AssignByBarcode([FromBody] AssignVehicleByBarcodeDto request)
    {
        var success = await Mediator.Send(new AssignVehicleByBarcodeCommand(
            request.GuestId, request.BarcodeValue, CurrentUserId, request.Notes));
        if (!success) return BadRequest(new { message = "Barcode not found or vehicle unavailable." });
        return NoContent();
    }
}
