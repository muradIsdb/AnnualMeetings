using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

/// <summary>
/// TEMPORARY: Admin reset endpoint for UAT data cleanup.
/// TODO: Remove this controller after use.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminResetController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminResetController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Deletes ALL TravelBookings, TravelBookingHistory, Flights, and Guests from the database.
    /// Use only in UAT for a clean re-sync from EventsAir.
    /// </summary>
    [HttpPost("reset-data")]
    public async Task<IActionResult> ResetData(CancellationToken ct)
    {
        // Delete in dependency order to avoid FK violations
        var travelHistory = await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"TravelBookingHistories\"", ct);
        var travelBookings = await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"TravelBookings\"", ct);
        var flights = await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Flights\"", ct);
        var guests = await _db.Database.ExecuteSqlRawAsync("DELETE FROM \"Guests\"", ct);

        return Ok(new
        {
            message = "Reset complete",
            travelHistoryDeleted = travelHistory,
            travelBookingsDeleted = travelBookings,
            flightsDeleted = flights,
            guestsDeleted = guests
        });
    }
}
