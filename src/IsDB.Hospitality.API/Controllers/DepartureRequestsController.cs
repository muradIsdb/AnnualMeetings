using IsDB.Hospitality.Application.DTOs.DepartureRequests;
using IsDB.Hospitality.Application.Features.DepartureRequests.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsDB.Hospitality.API.Controllers;

public class DepartureRequestsController : ApiControllerBase
{
    /// <summary>
    /// Public endpoint — no authentication required. Used by the hotel QR code form.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<Guid>> CreateDepartureRequest([FromBody] CreateDepartureRequestDto request)
    {
        var id = await Mediator.Send(new CreateDepartureRequestCommand(
            request.GuestName,
            request.GuestEmail,
            request.GuestPhone,
            request.HotelName,
            request.RoomNumber,
            request.RequestedPickupTime,
            request.DestinationAirport,
            request.FlightNumber,
            request.SpecialRequirements));

        return CreatedAtAction(nameof(CreateDepartureRequest), new { id }, new { id, message = "Your departure request has been received. Our team will contact you shortly." });
    }
}
