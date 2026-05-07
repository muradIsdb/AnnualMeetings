using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Services;

/// <summary>
/// Resolves notification templates from the database and provides helpers
/// to build Notification entities from a template + context values.
/// Also handles seeding default templates on first run.
/// </summary>
public class NotificationTemplateService
{
    private readonly AppDbContext _db;

    public NotificationTemplateService(AppDbContext db)
    {
        _db = db;
    }

    // ─── Default templates ────────────────────────────────────────────────────
    public static readonly IReadOnlyList<NotificationTemplate> DefaultTemplates = new List<NotificationTemplate>
    {
        new()
        {
            EventKey     = "inbound.arrived",
            EventLabel   = "Guest Arrived at Airport",
            MessageTemplate = "[Inbound] {GuestName} has arrived at the airport.",
            TargetRoles  = "Transport",
            Priority     = AlertSeverity.Critical,
            Description  = "Fires when Airport team marks a guest as Arrived."
        },
        new()
        {
            EventKey     = "inbound.arrived.hotel_copy",
            EventLabel   = "Guest Arrived at Airport (Hotel copy)",
            MessageTemplate = "[Inbound] {GuestName} has arrived at the airport.",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.High,
            Description  = "Hotel copy of the Arrived notification."
        },
        new()
        {
            EventKey     = "inbound.arrived.admin_copy",
            EventLabel   = "Guest Arrived at Airport (Admin copy)",
            MessageTemplate = "[Inbound] {GuestName} has arrived at the airport.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.Critical,
            Description  = "Admin copy of the Arrived notification."
        },
        new()
        {
            EventKey     = "inbound.received_by_embassy",
            EventLabel   = "Guest Received by Embassy Team",
            MessageTemplate = "[Inbound] {GuestName} received by Embassy team.",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.High,
            Description  = "Fires when Transport marks a guest as received by the Embassy team."
        },
        new()
        {
            EventKey     = "inbound.received_by_embassy.admin_copy",
            EventLabel   = "Guest Received by Embassy Team (Admin copy)",
            MessageTemplate = "[Inbound] {GuestName} received by Embassy team.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.High,
            Description  = "Admin copy of the Received by Embassy notification."
        },
        new()
        {
            EventKey     = "inbound.vehicle_assigned",
            EventLabel   = "Vehicle Assigned (First Time)",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle was assigned (dispatched from Airport).",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.Critical,
            Description  = "Fires when a vehicle is assigned to a guest for the first time."
        },
        new()
        {
            EventKey     = "inbound.vehicle_assigned.admin_copy",
            EventLabel   = "Vehicle Assigned (First Time) — Admin copy",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle was assigned (dispatched from Airport).",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.Critical,
            Description  = "Admin copy of the Vehicle Assigned notification."
        },
        new()
        {
            EventKey     = "inbound.vehicle_changed",
            EventLabel   = "Vehicle Changed (Reassignment)",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle was changed.",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.Low,
            Description  = "Fires when a guest's vehicle is swapped for a different one."
        },
        new()
        {
            EventKey     = "inbound.vehicle_changed.admin_copy",
            EventLabel   = "Vehicle Changed (Reassignment) — Admin copy",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle was changed.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.Low,
            Description  = "Admin copy of the Vehicle Changed notification."
        },
        new()
        {
            EventKey     = "inbound.vehicle_cancelled",
            EventLabel   = "Vehicle Assignment Cancelled",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle assignment was cancelled.",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.Critical,
            Description  = "Fires when Transport unassigns a vehicle from a guest."
        },
        new()
        {
            EventKey     = "inbound.vehicle_cancelled.admin_copy",
            EventLabel   = "Vehicle Assignment Cancelled — Admin copy",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle assignment was cancelled.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.Critical,
            Description  = "Admin copy of the Vehicle Assignment Cancelled notification."
        },
        new()
        {
            EventKey     = "inbound.vehicle_status_changed",
            EventLabel   = "Vehicle Assigned (Status Auto-Set)",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle has been dispatched.",
            TargetRoles  = "Hotel",
            Priority     = AlertSeverity.High,
            Description  = "Fires when the inbound status is auto-set to VehicleAssigned after arrival."
        },
        new()
        {
            EventKey     = "inbound.vehicle_status_changed.admin_copy",
            EventLabel   = "Vehicle Assigned (Status Auto-Set) — Admin copy",
            MessageTemplate = "[Inbound] {GuestName}'s vehicle has been dispatched.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.High,
            Description  = "Admin copy of the Vehicle Status Changed notification."
        },
        new()
        {
            EventKey     = "outbound.in_transfer",
            EventLabel   = "Guest In Transfer to Airport",
            MessageTemplate = "[Outbound] {GuestName} is in transfer to the airport.",
            TargetRoles  = "Transport",
            Priority     = AlertSeverity.High,
            Description  = "Fires when Hotel marks a guest as In Transfer."
        },
        new()
        {
            EventKey     = "outbound.in_transfer.admin_copy",
            EventLabel   = "Guest In Transfer to Airport — Admin copy",
            MessageTemplate = "[Outbound] {GuestName} is in transfer to the airport.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.High,
            Description  = "Admin copy of the In Transfer notification."
        },
        new()
        {
            EventKey     = "outbound.at_airport",
            EventLabel   = "Guest Arrived at Departure Terminal",
            MessageTemplate = "[Outbound] {GuestName} has arrived at the departure terminal.",
            TargetRoles  = "Airport",
            Priority     = AlertSeverity.Critical,
            Description  = "Fires when Transport marks a guest as At Airport."
        },
        new()
        {
            EventKey     = "outbound.at_airport.admin_copy",
            EventLabel   = "Guest Arrived at Departure Terminal — Admin copy",
            MessageTemplate = "[Outbound] {GuestName} has arrived at the departure terminal.",
            TargetRoles  = "Admin",
            Priority     = AlertSeverity.Critical,
            Description  = "Admin copy of the At Airport notification."
        },
    };

    // ─── Seed defaults if not present ────────────────────────────────────────
    public async Task SeedDefaultsAsync()
    {
        var existingKeys = await _db.NotificationTemplates
            .Select(t => t.EventKey)
            .ToListAsync();

        var toInsert = DefaultTemplates
            .Where(t => !existingKeys.Contains(t.EventKey))
            .ToList();

        if (toInsert.Count > 0)
        {
            _db.NotificationTemplates.AddRange(toInsert);
            await _db.SaveChangesAsync();
        }
    }

    // ─── Resolve a template by event key ─────────────────────────────────────
    public async Task<NotificationTemplate?> GetTemplateAsync(string eventKey)
    {
        return await _db.NotificationTemplates
            .FirstOrDefaultAsync(t => t.EventKey == eventKey);
    }

    // ─── Build a Notification from a template + context ──────────────────────
    public async Task<Notification?> BuildNotificationAsync(
        string eventKey,
        string guestName,
        Guid createdByStaffId,
        string? vehiclePlate = null,
        string? vehicleMake = null,
        string? vehicleModel = null)
    {
        var template = await GetTemplateAsync(eventKey);
        if (template == null) return null;

        var message = template.MessageTemplate
            .Replace("{GuestName}", guestName)
            .Replace("{VehiclePlate}", vehiclePlate ?? "")
            .Replace("{VehicleMake}", vehicleMake ?? "")
            .Replace("{VehicleModel}", vehicleModel ?? "");

        return new Notification
        {
            Message = message,
            TargetRoles = template.TargetRoles,
            Priority = template.Priority,
            CreatedByStaffId = createdByStaffId
        };
    }

    // ─── Build multiple notifications (e.g. team + admin copy) ───────────────
    public async Task<List<Notification>> BuildNotificationsAsync(
        IEnumerable<string> eventKeys,
        string guestName,
        Guid createdByStaffId,
        string? vehiclePlate = null,
        string? vehicleMake = null,
        string? vehicleModel = null)
    {
        var result = new List<Notification>();
        foreach (var key in eventKeys)
        {
            var n = await BuildNotificationAsync(key, guestName, createdByStaffId, vehiclePlate, vehicleMake, vehicleModel);
            if (n != null) result.Add(n);
        }
        return result;
    }
}
