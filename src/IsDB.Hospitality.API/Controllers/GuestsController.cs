using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.Common.Models;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Application.Features.Guests.Commands;
using IsDB.Hospitality.Application.Features.Guests.Queries;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IsDB.Hospitality.API.Controllers;

[Authorize]
public class GuestsController : ApiControllerBase
{
    // In-memory store for background sync job status
    private static readonly ConcurrentDictionary<string, SyncJobStatus> _syncJobs = new();

    // Well-known custom field GUIDs
    private const string DEDICATED_CAR_FIELD_GUID = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
    private const string RANK_FIELD_GUID = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";

    [HttpGet("arrival-flights")]
    public async Task<ActionResult<List<ArrivalFlightGroupDto>>> GetArrivalFlights(
        [FromServices] AppDbContext db = null!,
        CancellationToken ct = default)
    {
        var activeEventCode = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetArrivalFlightsQuery(activeEventCode));
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<GuestSummaryDto>>> GetGuests(
        [FromQuery] GuestStatus? status = null,
        [FromQuery] bool? isCritical = null,
        [FromServices] AppDbContext db = null!,
        CancellationToken ct = default)
    {
        var activeEventCode = (await db.EventsAirConfigs.FirstOrDefaultAsync(ct))?.EventCode;
        var result = await Mediator.Send(new GetGuestsQuery(status, isCritical, activeEventCode));
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

    [HttpGet("inactive")]
    public async Task<IActionResult> GetInactiveGuests([FromServices] AppDbContext db, CancellationToken ct)
    {
        var guests = await db.Guests
            .Where(g => !g.IsActive)
            .OrderBy(g => g.LastName).ThenBy(g => g.FirstName)
            .Select(g => new
            {
                g.Id,
                g.FirstName,
                g.LastName,
                g.RegistrationTypeName,
                g.Organization,
                g.Email,
                g.LastSyncedAt
            })
            .ToListAsync(ct);
        return Ok(guests);
    }

    [HttpDelete("inactive/all")]
    public async Task<IActionResult> DeleteAllInactiveGuests([FromServices] AppDbContext db, CancellationToken ct)
    {
        var inactive = await db.Guests.Where(g => !g.IsActive).ToListAsync(ct);
        var count = inactive.Count;
        db.Guests.RemoveRange(inactive);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = $"{count} inactive participant(s) permanently deleted.", deleted = count });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteGuest(Guid id, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var guest = await db.Guests.FindAsync(new object[] { id }, ct);
        if (guest == null) return NotFound();
        db.Guests.Remove(guest);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Participant permanently deleted." });
    }

    /// <summary>
    /// Bulk-assign a car class to multiple guests at once.
    /// If a guest already has a vehicle assigned from a different class, a warning is included.
    /// </summary>
    [HttpPost("bulk-assign-car-class")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BulkAssignCarClass(
        [FromBody] BulkAssignCarClassRequest req,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        if (req.GuestIds == null || req.GuestIds.Count == 0)
            return BadRequest(new { message = "No guests specified." });

        // Validate car class exists (null = remove class)
        if (req.CarClassId.HasValue)
        {
            var classExists = await db.CarClasses.AnyAsync(c => c.Id == req.CarClassId.Value, ct);
            if (!classExists) return BadRequest(new { message = "Car class not found." });
        }

        var guests = await db.Guests
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
                .ThenInclude(va => va.Vehicle)
                    .ThenInclude(v => v.CarClass)
            .Where(g => req.GuestIds.Contains(g.Id) && g.IsActive)
            .ToListAsync(ct);

        var warnings = new List<string>();
        int updated = 0;

        foreach (var guest in guests)
        {
            var activeAssignment = guest.VehicleAssignments.FirstOrDefault(va => va.IsActive);
            if (activeAssignment != null && req.CarClassId.HasValue)
            {
                var vehicleClassId = activeAssignment.Vehicle?.CarClassId;
                if (vehicleClassId.HasValue && vehicleClassId != req.CarClassId)
                {
                    warnings.Add($"{guest.FirstName} {guest.LastName} already has a vehicle assigned from a different class.");
                }
            }
            guest.DeservedCarClassId = req.CarClassId;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { updated, warnings });
    }

    /// <summary>
    /// Optimized sync flow:
    /// Pass 1: Fetch contacts with DedicatedCar=True from EventsAir using checkboxCustomFieldFilters.
    ///         This returns only contacts who have the Dedicated Car checkbox checked.
    ///         Rank is fetched inline via customFields in the same query.
    /// Pass 2: Deactivate guests not in the fetched set (unless they have active vehicle assignments).
    /// Pass 3: Fetch and sync travel bookings for active guests.
    /// </summary>
    [HttpPost("sync-from-eventsair")]
    public async Task<IActionResult> SyncFromEventsAir(
        [FromServices] AppDbContext db,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IMemoryCache cache,
        [FromServices] IServiceScopeFactory scopeFactory,
        [FromServices] ISystemLogService systemLogService,
        CancellationToken cancellationToken)
    {
        var config = await db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive || string.IsNullOrWhiteSpace(config.ClientId))
        {
            return BadRequest(new { message = "EventsAir integration is not configured or inactive." });
        }

        var jobId = Guid.NewGuid().ToString("N");
        var job = new SyncJobStatus { JobId = jobId, State = "running", StartedAt = DateTime.UtcNow };
        _syncJobs[jobId] = job;

        var clientId = config.ClientId;
        var clientSecret = config.ClientSecret;
        var eventCode = config.EventCode;
        var apiBaseUrl = config.ApiBaseUrl;
        // OAuthScope is NotMapped on EventsAirConfig entity — read via raw SQL
        string oAuthScope;
        try
        {
            var conn2 = db.Database.GetDbConnection();
            if (conn2.State != System.Data.ConnectionState.Open) await conn2.OpenAsync(cancellationToken);
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "SELECT \"OAuthScope\" FROM \"EventsAirConfigs\" LIMIT 1";
            var scopeResult2 = await cmd2.ExecuteScalarAsync(cancellationToken);
            oAuthScope = scopeResult2 is string s2 && !string.IsNullOrWhiteSpace(s2)
                ? s2
                : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        }
        catch
        {
            oAuthScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        }

        // Load custom field GUIDs from DB filtered by active event code (prefer event-specific over global NULL)
        var fieldMappings = await db.SyncFieldMappings
            .Where(f => f.EventCode == null || f.EventCode == eventCode)
            .ToListAsync(cancellationToken);
        var dedicatedCarGuid = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase) && f.EventCode == eventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? DEDICATED_CAR_FIELD_GUID;
        var rankGuid = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase) && f.EventCode == eventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? RANK_FIELD_GUID;
        var vehicleTypeGuid = (fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Vehicle Types", StringComparison.OrdinalIgnoreCase) && f.EventCode == eventCode)
            ?? fieldMappings.FirstOrDefault(f =>
                f.DisplayName.Equals("Vehicle Types", StringComparison.OrdinalIgnoreCase)))
            ?.EventsAirFieldGuid ?? VEHICLE_TYPE_FIELD_GUID;

        // Capture caller identity before entering the background Task (HttpContext not available inside)
        var callerStaffIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var callerStaffId = callerStaffIdClaim != null && Guid.TryParse(callerStaffIdClaim, out var csid) ? csid : (Guid?)null;
        var callerStaffName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                           ?? User.FindFirst("name")?.Value
                           ?? User.Identity?.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                var token = await GetEventsAirTokenAsync(clientId, clientSecret, httpClientFactory, cache, oAuthScope);
                Console.WriteLine($"[SYNC] Token acquired. Starting optimized sync...");

                // ═══════════════════════════════════════════════════════════════
                // PASS 1: Fetch contacts with DedicatedCar=True (includes Rank)
                // ═══════════════════════════════════════════════════════════════
                var contacts = await FetchContactsWithDedicatedCarAsync(
                    apiBaseUrl, eventCode, token, httpClientFactory, CancellationToken.None, dedicatedCarGuid, rankGuid, vehicleTypeGuid);

                job.TotalFetched = contacts.Count;
                Console.WriteLine($"[SYNC] Pass 1 complete: {contacts.Count} contacts with DedicatedCar=True fetched.");

                using var scope = scopeFactory.CreateScope();
                var bgDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                int added = 0, updated = 0, deactivated = 0;
                 var syncedContactIds = new HashSet<string>(contacts.Select(c => c.ContactId), StringComparer.OrdinalIgnoreCase);

                // ── Bulk-load ALL guests keyed by EventsAirContactId (eliminates N per-contact SELECT) ──
                var existingGuestsByContactId = await bgDb.Guests
                    .ToDictionaryAsync(g => g.EventsAirContactId, StringComparer.OrdinalIgnoreCase);

                // ── Bulk-load CarClasses for the current event keyed by Name (case-insensitive) ──
                var carClassesByName = (await bgDb.CarClasses
                    .Where(cc => cc.EventCode == eventCode)
                    .ToListAsync())
                    .GroupBy(cc => cc.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                int vehicleTypeMatched = 0, vehicleTypeUnmatched = 0;
                var vehicleTypeUnmatchedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Upsert guests
                foreach (var contact in contacts)
                {
                    if (string.IsNullOrEmpty(contact.ContactId)) continue;

                    // Resolve DeservedCarClassId from VehicleTypeValue
                    Guid? resolvedCarClassId = null;
                    if (!string.IsNullOrWhiteSpace(contact.VehicleTypeValue))
                    {
                        var trimmedVehicleType = contact.VehicleTypeValue.Trim();
                        if (carClassesByName.TryGetValue(trimmedVehicleType, out var matchedCarClass))
                        {
                            resolvedCarClassId = matchedCarClass.Id;
                            vehicleTypeMatched++;
                        }
                        else
                        {
                            vehicleTypeUnmatched++;
                            vehicleTypeUnmatchedValues.Add(trimmedVehicleType);
                        }
                    }

                    if (!existingGuestsByContactId.TryGetValue(contact.ContactId, out var existing))
                    {
                        var newGuest = new Guest
                        {
                            EventsAirContactId = contact.ContactId,
                            FirstName = contact.FirstName,
                            LastName = contact.LastName,
                            Title = contact.Title,
                            Designation = contact.JobTitle,
                            Organization = contact.OrganizationName,
                            Email = contact.PrimaryEmail,
                            Country = contact.Country,
                            PhotoUrl = contact.PhotoUrl,
                            RegistrationTypeId = contact.RegistrationTypeId,
                            RegistrationTypeName = contact.RegistrationTypeName,
                            DedicatedCar = "True",
                            RankValue = contact.RankValue,
                            VehicleTypeValue = contact.VehicleTypeValue,
                            DeservedCarClassId = resolvedCarClassId,
                            IsActive = true,
                            Status = GuestStatus.Expected,
                            EventCode = eventCode,
                            LastSyncedAt = DateTime.UtcNow
                        };
                        bgDb.Guests.Add(newGuest);
                        existingGuestsByContactId[contact.ContactId] = newGuest; // keep dict in sync
                        added++;
                    }
                    else
                    {
                        bool changed = false;
                        if (existing.FirstName != contact.FirstName) { existing.FirstName = contact.FirstName; changed = true; }
                        if (existing.LastName != contact.LastName) { existing.LastName = contact.LastName; changed = true; }
                        if (existing.Designation != contact.JobTitle) { existing.Designation = contact.JobTitle; changed = true; }
                        if (existing.Organization != contact.OrganizationName) { existing.Organization = contact.OrganizationName; changed = true; }
                        // ── Detect registration type change → create SyncAlert ──────────────
                        if (existing.RegistrationTypeName != contact.RegistrationTypeName)
                        {
                            if (!string.IsNullOrWhiteSpace(existing.RegistrationTypeName) && !string.IsNullOrWhiteSpace(contact.RegistrationTypeName))
                            {
                                bgDb.SyncAlerts.Add(new IsDB.Hospitality.Domain.Entities.SyncAlert
                                {
                                    AlertType    = IsDB.Hospitality.Domain.Enums.SyncAlertType.RegTypeChanged,
                                    GuestId      = existing.Id,
                                    GuestName    = $"{existing.FirstName} {existing.LastName}".Trim(),
                                    EventsAirContactId = existing.EventsAirContactId,
                                    OldValue     = existing.RegistrationTypeName,
                                    NewValue     = contact.RegistrationTypeName,
                                    SyncSource   = IsDB.Hospitality.Domain.Enums.SyncAlertSource.ManualSync,
                                    DetectedAt   = DateTime.UtcNow
                                });
                            }
                            existing.RegistrationTypeName = contact.RegistrationTypeName;
                            changed = true;
                        }
                        if (existing.RegistrationTypeId != contact.RegistrationTypeId) { existing.RegistrationTypeId = contact.RegistrationTypeId; changed = true; }
                        if (existing.Email != contact.PrimaryEmail) { existing.Email = contact.PrimaryEmail; changed = true; }
                        if (existing.Country != contact.Country) { existing.Country = contact.Country; changed = true; }
                        if (existing.PhotoUrl != contact.PhotoUrl) { existing.PhotoUrl = contact.PhotoUrl; changed = true; }
                        if (existing.RankValue != contact.RankValue) { existing.RankValue = contact.RankValue; changed = true; }
                        if (existing.VehicleTypeValue != contact.VehicleTypeValue) { existing.VehicleTypeValue = contact.VehicleTypeValue; changed = true; }
                        // Always overwrite DeservedCarClassId from VehicleTypeValue on every sync
                        if (existing.DeservedCarClassId != resolvedCarClassId) { existing.DeservedCarClassId = resolvedCarClassId; changed = true; }
                        if (existing.DedicatedCar != "True") { existing.DedicatedCar = "True"; changed = true; }
                        if (!existing.IsActive) { existing.IsActive = true; changed = true; }
                        // Stamp EventCode if not already set or if it differs from the active event
                        if (existing.EventCode != eventCode) { existing.EventCode = eventCode; changed = true; }
                        if (changed) { existing.LastSyncedAt = DateTime.UtcNow; updated++; }
                    }
                }
                await bgDb.SaveChangesAsync();
                Console.WriteLine($"[SYNC] Upsert complete: {added} new, {updated} updated. VehicleType: {vehicleTypeMatched} matched, {vehicleTypeUnmatched} unmatched ({string.Join(", ", vehicleTypeUnmatchedValues.Take(10))})");

                // ═══════════════════════════════════════════════════════════════
                // PASS 2: Deactivate guests not in the fetched set
                // ═══════════════════════════════════════════════════════════════
                // ── Load active vehicle assignments for release on deactivation ──
                var activeAssignmentsManual = await bgDb.VehicleAssignments
                    .Where(va => va.IsActive)
                    .Include(va => va.Vehicle)
                    .ToListAsync();
                var assignmentsByGuestManual = activeAssignmentsManual
                    .GroupBy(va => va.GuestId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var kvp in existingGuestsByContactId)
                {
                    var guestToDeactivate = kvp.Value;
                    if (!string.IsNullOrEmpty(guestToDeactivate.EventsAirContactId) &&
                        !syncedContactIds.Contains(guestToDeactivate.EventsAirContactId) &&
                        guestToDeactivate.IsActive)
                    {
                        // ── Release active vehicle assignment if present ──────────────
                        string? vehiclePlateManual = null;
                        Guid? vehicleIdManual = null;
                        if (assignmentsByGuestManual.TryGetValue(guestToDeactivate.Id, out var assignmentManual))
                        {
                            assignmentManual.IsActive = false;
                            assignmentManual.UnassignedAt = DateTime.UtcNow;
                            vehiclePlateManual = assignmentManual.Vehicle?.LicensePlate;
                            vehicleIdManual = assignmentManual.VehicleId;
                            if (assignmentManual.Vehicle != null)
                                assignmentManual.Vehicle.Status = IsDB.Hospitality.Domain.Enums.VehicleStatus.Available;
                        }
                        // ── Create GuestRemoved SyncAlert ──────────────────────────────
                        bgDb.SyncAlerts.Add(new IsDB.Hospitality.Domain.Entities.SyncAlert
                        {
                            AlertType    = IsDB.Hospitality.Domain.Enums.SyncAlertType.GuestRemoved,
                            GuestId      = guestToDeactivate.Id,
                            GuestName    = $"{guestToDeactivate.FirstName} {guestToDeactivate.LastName}".Trim(),
                            EventsAirContactId = guestToDeactivate.EventsAirContactId,
                            VehicleId    = vehicleIdManual,
                            VehiclePlate = vehiclePlateManual,
                            SyncSource   = IsDB.Hospitality.Domain.Enums.SyncAlertSource.ManualSync,
                            DetectedAt   = DateTime.UtcNow
                        });
                        guestToDeactivate.IsActive = false;
                        guestToDeactivate.DedicatedCar = null;
                        guestToDeactivate.LastSyncedAt = DateTime.UtcNow;
                        deactivated++;
                    }
                }
                await bgDb.SaveChangesAsync();
                Console.WriteLine($"[SYNC] Pass 2 complete: {deactivated} deactivated.");

                // ═══════════════════════════════════════════════════════════════
                // PASS 3: Travel Sync (replace-on-rebooking with history)
                // Each guest has at most ONE arrival booking and ONE departure booking.
                // If the flight number changes, the old booking is saved to history
                // and the booking is updated to point to the new flight.
                // Scheduled flight fields are ALWAYS overwritten from EventsAir.
                // ═══════════════════════════════════════════════════════════════
                try
                {
                    var travelBookings = await EventsAirSyncHelpers.FetchTravelBookingsByContactsAsync(apiBaseUrl, eventCode, token, httpClientFactory, syncedContactIds, CancellationToken.None);
                    Console.WriteLine($"[TRAVEL-SYNC] Processing {travelBookings.Count} travel bookings...");
                    foreach (var sample in travelBookings.Take(5))
                        Console.WriteLine($"[TRAVEL-SYNC] Sample: ContactId={sample.ContactId}, FlightNumber={sample.FlightNumber}, TravelType={sample.TravelTypeName}, ArrivalDate={sample.ArrivalDate}");

                    // ── Delegate all flight+booking processing to the shared helper ────────────
                    // This is the single source of truth for flight deduplication rules.
                    var syncResult = await EventsAirSyncHelpers.ProcessTravelBookingsAsync(bgDb, travelBookings);

                    // ── Orphan cleanup: delete flight rows with no remaining bookings ──────
                    // When a guest's date or time changes, the old flight row may be left with
                    // no bookings. Clean it up here to keep the Flights table tidy.
                    var orphanFlightIds = await bgDb.Flights
                        .Where(f => !bgDb.TravelBookings.Any(tb => tb.FlightId == f.Id))
                        .Select(f => f.Id)
                        .ToListAsync();
                    if (orphanFlightIds.Count > 0)
                    {
                        var orphans = await bgDb.Flights.Where(f => orphanFlightIds.Contains(f.Id)).ToListAsync();
                        bgDb.Flights.RemoveRange(orphans);
                        Console.WriteLine($"[TRAVEL-SYNC] Orphan cleanup: removed {orphans.Count} flight rows with no bookings.");
                    }

                    await bgDb.SaveChangesAsync();

                    Console.WriteLine($"[TRAVEL-SYNC] Results: {syncResult.SavedNew} new, {syncResult.UpdatedExisting} updated, {syncResult.Rebooked} rebooked, {syncResult.ErrorCount} errors, skipped: {syncResult.SkippedNoFlight} no flight, {syncResult.SkippedNoContact} no contact, {syncResult.SkippedNoGuest} no guest match");
                    foreach (var err in syncResult.Errors)
                        Console.WriteLine($"[TRAVEL-SYNC] Error: {err}");

                    // Store diagnostics on job so sync-status endpoint can return them
                    job.TravelFetched          = travelBookings.Count;
                    job.TravelSavedNew         = syncResult.SavedNew;
                    job.TravelUpdated          = syncResult.UpdatedExisting;
                    job.TravelRebooked         = syncResult.Rebooked;
                    job.TravelSkippedNoFlight  = syncResult.SkippedNoFlight;
                    job.TravelSkippedNoContact = syncResult.SkippedNoContact;
                    job.TravelSkippedNoGuest   = syncResult.SkippedNoGuest;
                    job.TravelErrors           = syncResult.ErrorCount;
                    job.ConflictAlertCount     = syncResult.ConflictAlertCount;
                }
                catch (Exception ex)
                {
                    job.TravelFirstError = ex.Message;
                    Console.WriteLine($"Travel sync error: {ex.Message}\n{ex.StackTrace}");
                }

                job.Added = added; job.Updated = updated; job.Deactivated = deactivated;
                job.VehicleTypeMatched = vehicleTypeMatched;
                job.VehicleTypeUnmatched = vehicleTypeUnmatched;
                job.VehicleTypeUnmatchedValues = vehicleTypeUnmatchedValues.ToList();
                job.State = "done"; job.FinishedAt = DateTime.UtcNow;
                var unmatchedSuffix = vehicleTypeUnmatched > 0
                    ? $" VehicleType: {vehicleTypeMatched} matched, {vehicleTypeUnmatched} unmatched ({string.Join(", ", vehicleTypeUnmatchedValues.Take(5))})"
                    : $" VehicleType: {vehicleTypeMatched} matched.";
                job.Message = $"Sync complete. {added} new, {updated} updated, {deactivated} deactivated. Travel: {job.TravelSavedNew} new, {job.TravelUpdated} updated, {job.TravelRebooked} rebooked.{unmatchedSuffix}";
                Console.WriteLine($"[SYNC] All passes complete. {added} new, {updated} updated, {deactivated} deactivated.");

                // ── Write comprehensive sync log entry ────────────────────────
                try
                {
                    using var logScope = scopeFactory.CreateScope();
                    var logDb = logScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var syncConfig = await logDb.EventsAirConfigs.FirstOrDefaultAsync();
                    if (syncConfig != null)
                    {
                        syncConfig.LastSyncAt = DateTime.UtcNow;
                        syncConfig.LastSyncStatus = "Success";
                        syncConfig.LastSyncMessage = job.Message;
                        syncConfig.LastSyncRecordsCount = added + updated;
                        syncConfig.LastSyncDeactivatedCount = deactivated;
                    }
                    logDb.EventsAirSyncLogs.Add(new IsDB.Hospitality.Domain.Entities.EventsAirSyncLog
                    {
                        SyncedAt = DateTime.UtcNow,
                        Status = "Success",
                        Message = job.Message,
                        RecordsSynced = added + updated,
                        DurationMs = (int)(job.FinishedAt!.Value - job.StartedAt).TotalMilliseconds,
                        SyncType = "Manual",
                        TriggerSource = "Admin UI Button",
                        InitiatedByStaffId = callerStaffId,
                        InitiatedByStaffName = callerStaffName,
                        RecordsAdded = added,
                        RecordsUpdated = updated,
                        RecordsDeactivated = deactivated,
                        TravelBookingsSynced = job.TravelSavedNew + job.TravelUpdated + job.TravelRebooked
                    });
                    await logDb.SaveChangesAsync();
                }
                catch (Exception logEx)
                {
                    Console.WriteLine($"[SYNC] Warning: could not write sync log: {logEx.Message}");
                }
                // ── Write to System Logs (success) ────────────────────────────
                await systemLogService.LogAsync(
                    LogSeverity.Information,
                    "EventsAir Sync",
                    $"Manual sync completed: {added} added, {updated} updated, {deactivated} deactivated",
                    job.Message,
                    null,
                    callerStaffId,
                    callerStaffName);
            }
            catch (Exception ex)
            {
                job.State = "error"; job.Message = ex.Message; job.FinishedAt = DateTime.UtcNow;
                Console.WriteLine($"[SYNC] Error: {ex.Message}\n{ex.StackTrace}");
                // ── Write to System Logs (failure) ────────────────────────────
                await systemLogService.LogAsync(
                    LogSeverity.Error,
                    "EventsAir Sync",
                    "Manual sync failed",
                    ex.Message,
                    null,
                    callerStaffId,
                    callerStaffName);
                // ── Write failure log entry ───────────────────────────────────
                try
                {
                    using var logScope = scopeFactory.CreateScope();
                    var logDb = logScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    logDb.EventsAirSyncLogs.Add(new IsDB.Hospitality.Domain.Entities.EventsAirSyncLog
                    {
                        SyncedAt = DateTime.UtcNow,
                        Status = "Failed",
                        Message = ex.Message,
                        RecordsSynced = 0,
                        DurationMs = (int)(DateTime.UtcNow - job.StartedAt).TotalMilliseconds,
                        SyncType = "Manual",
                        TriggerSource = "Admin UI Button",
                        InitiatedByStaffId = callerStaffId,
                        InitiatedByStaffName = callerStaffName
                    });
                    await logDb.SaveChangesAsync();
                }
                catch { /* best-effort */ }
            }
        });

        return Ok(new { jobId });
    }

    [HttpGet("sync-status/{jobId}")]
    public IActionResult GetSyncStatus(string jobId)
    {
        if (!_syncJobs.TryGetValue(jobId, out var job)) return NotFound();
        return Ok(job);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper: Get OAuth2 token for EventsAir API
    // ═══════════════════════════════════════════════════════════════════════════
    private static async Task<string> GetEventsAirTokenAsync(string clientId, string clientSecret, IHttpClientFactory httpClientFactory, IMemoryCache cache, string? oAuthScope = null)
    {
        var cacheKey = $"eventsair_token_{clientId}";
        if (cache.TryGetValue(cacheKey, out string? cachedToken) && cachedToken != null) return cachedToken;
        var client = httpClientFactory.CreateClient();
        var tokenUrl = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
        var scope = !string.IsNullOrWhiteSpace(oAuthScope)
            ? oAuthScope
            : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        var tokenRequest = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", scope)
        });
        var response = await client.PostAsync(tokenUrl, tokenRequest);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var token = doc.GetProperty("access_token").GetString()!;
        var expiresIn = doc.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        cache.Set(cacheKey, token, TimeSpan.FromSeconds(expiresIn - 60));
        return token;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OPTIMIZED: Fetch contacts with DedicatedCar=True using checkboxCustomFieldFilters
    // Returns contacts with their Rank value included (from customFields inline)
    // Only 2-3 API calls needed for ~200 contacts
    // ═══════════════════════════════════════════════════════════════════════════
    private const string VEHICLE_TYPE_FIELD_GUID = "5f6b0e9e-7d1c-4f91-affc-ecbe95cef678";

    private static async Task<List<EventsAirContactDto>> FetchContactsWithDedicatedCarAsync(
        string baseUrl, string eventCode, string accessToken, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken,
        string? dedicatedCarFieldGuid = null, string? rankFieldGuid = null, string? vehicleTypeFieldGuid = null)
    {
        dedicatedCarFieldGuid ??= DEDICATED_CAR_FIELD_GUID;
        rankFieldGuid ??= RANK_FIELD_GUID;
        vehicleTypeFieldGuid ??= VEHICLE_TYPE_FIELD_GUID;
        var fetched = new List<EventsAirContactDto>();
        var seenContactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        int offset = 0;
        const int pageSize = 25; // Must be ≤25 to stay under EventsAir's 10000 cost limit with customFields+registrations

        while (true)
        {
            // Use checkboxCustomFieldFilters to only get contacts with DedicatedCar=True
            // Include customFields to get Rank value inline (no separate per-contact query needed)
            var graphqlQuery = $@"{{
              event(id: ""{eventCode}"") {{
                contacts(input: {{ contactFilter: {{ customFields: {{ checkboxCustomFieldFilters: [{{ definitionId: ""{dedicatedCarFieldGuid}"", isChecked: true }}] }} }} }}, offset: {offset}, limit: {pageSize}) {{
                  id
                  firstName
                  lastName
                  title
                  jobTitle
                  organizationName
                  primaryEmail
                  primaryAddress {{ country }}
                  photo {{ url }}
                  customFields {{ definitionId value }}
                  registrations {{ type {{ id name }} }}
                }}
              }}
            }}";

            var queryBody = JsonSerializer.Serialize(new { query = graphqlQuery });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[SYNC] HTTP {(int)response.StatusCode} at offset {offset}: {json[..Math.Min(json.Length, 500)]}");
                throw new InvalidOperationException($"EventsAir API returned HTTP {(int)response.StatusCode}");
            }

            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var errorMsg = errors[0].GetProperty("message").GetString() ?? "Unknown GraphQL error";
                Console.WriteLine($"[SYNC] GraphQL error at offset {offset}: {errorMsg}");

                // If cost limit exceeded, retry with fewer fields
                if (errorMsg.Contains("cost", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[SYNC] Cost limit hit — retrying with lighter query (no customFields/photo)...");
                    return await FetchContactsWithDedicatedCarLightAsync(baseUrl, eventCode, accessToken, httpClientFactory, cancellationToken, dedicatedCarFieldGuid, rankFieldGuid, vehicleTypeFieldGuid);
                }
                throw new InvalidOperationException($"GraphQL error: {errorMsg}");
            }

            var contacts = doc.GetProperty("data").GetProperty("event").GetProperty("contacts");
            int pageCount = 0;

            foreach (var contact in contacts.EnumerateArray())
            {
                pageCount++;
                var contactId = contact.TryGetProperty("id", out var cidEl) ? cidEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(contactId) || !seenContactIds.Add(contactId)) continue;

                // Extract Rank and VehicleType from customFields
                string? rankValue = null;
                string? vehicleTypeValue = null;
                if (contact.TryGetProperty("customFields", out var cfArray) && cfArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cf in cfArray.EnumerateArray())
                    {
                        var defId = cf.TryGetProperty("definitionId", out var did) ? did.GetString() ?? "" : "";
                        if (string.Equals(defId, rankFieldGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            if (cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null)
                                rankValue = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText().Trim('"');
                        }
                        else if (string.Equals(defId, vehicleTypeFieldGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            if (cf.TryGetProperty("value", out var vt) && vt.ValueKind != JsonValueKind.Null)
                                vehicleTypeValue = vt.ValueKind == JsonValueKind.String ? vt.GetString() : vt.GetRawText().Trim('"');
                        }
                    }
                }

                // Extract registration type from first registration
                string regTypeId = "", regTypeName = "";
                if (contact.TryGetProperty("registrations", out var regsEl) && regsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var reg in regsEl.EnumerateArray())
                    {
                        if (reg.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Object)
                        {
                            regTypeId = typeEl.TryGetProperty("id", out var tidEl) ? tidEl.GetString() ?? "" : "";
                            regTypeName = typeEl.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";
                            break; // Use first registration type
                        }
                    }
                }

                // Extract other fields
                string? country = null;
                if (contact.TryGetProperty("primaryAddress", out var addrEl) && addrEl.ValueKind == JsonValueKind.Object)
                    country = addrEl.TryGetProperty("country", out var cEl) && cEl.ValueKind != JsonValueKind.Null ? cEl.GetString() : null;

                string? photoUrl = null;
                if (contact.TryGetProperty("photo", out var photoEl) && photoEl.ValueKind == JsonValueKind.Object)
                    photoUrl = photoEl.TryGetProperty("url", out var urlEl) && urlEl.ValueKind != JsonValueKind.Null ? urlEl.GetString() : null;

                fetched.Add(new EventsAirContactDto(
                    ContactId: contactId,
                    FirstName: contact.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? "" : "",
                    LastName: contact.TryGetProperty("lastName", out var ln) ? ln.GetString() ?? "" : "",
                    Title: contact.TryGetProperty("title", out var t) ? t.GetString() : null,
                    JobTitle: contact.TryGetProperty("jobTitle", out var jt) ? jt.GetString() : null,
                    OrganizationName: contact.TryGetProperty("organizationName", out var org) ? org.GetString() : null,
                    PrimaryEmail: contact.TryGetProperty("primaryEmail", out var em) ? em.GetString() : null,
                    RegistrationTypeId: regTypeId,
                    RegistrationTypeName: regTypeName,
                    Country: country,
                    PhotoUrl: photoUrl,
                    RankValue: rankValue,
                    VehicleTypeValue: vehicleTypeValue
                ));
            }

            Console.WriteLine($"[SYNC] Page offset={offset}: {pageCount} contacts (total: {fetched.Count})");
            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        return fetched;
    }

    /// <summary>
    /// Fallback: lighter query without customFields/photo if cost limit is exceeded.
    /// Rank will be fetched separately per-contact in this case.
    /// </summary>
    private static async Task<List<EventsAirContactDto>> FetchContactsWithDedicatedCarLightAsync(
        string baseUrl, string eventCode, string accessToken, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken,
        string? dedicatedCarFieldGuid = null, string? rankFieldGuid = null, string? vehicleTypeFieldGuid = null)
    {
        dedicatedCarFieldGuid ??= DEDICATED_CAR_FIELD_GUID;
        rankFieldGuid ??= RANK_FIELD_GUID;
        vehicleTypeFieldGuid ??= VEHICLE_TYPE_FIELD_GUID;
        var fetched = new List<EventsAirContactDto>();
        var seenContactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        int offset = 0;
        const int pageSize = 50; // 50 is safe without customFields/photo

        while (true)
        {
            var graphqlQuery = $@"{{
              event(id: ""{eventCode}"") {{
                  contacts(input: {{ contactFilter: {{ customFields: {{ checkboxCustomFieldFilters: [{{ definitionId: ""{dedicatedCarFieldGuid}"", isChecked: true }}] }} }} }}, offset: {offset}, limit: {pageSize}) {{
                  id
                  firstName
                  lastName
                  title
                  jobTitle
                  organizationName
                  primaryEmail
                  primaryAddress {{ country }}
                }}
              }}
            }}";

            var queryBody = JsonSerializer.Serialize(new { query = graphqlQuery });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");

            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                throw new InvalidOperationException("GraphQL error in light query");

            var contacts = doc.GetProperty("data").GetProperty("event").GetProperty("contacts");
            int pageCount = 0;

            foreach (var contact in contacts.EnumerateArray())
            {
                pageCount++;
                var contactId = contact.TryGetProperty("id", out var cidEl) ? cidEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(contactId) || !seenContactIds.Add(contactId)) continue;

                string? country = null;
                if (contact.TryGetProperty("primaryAddress", out var addrEl) && addrEl.ValueKind == JsonValueKind.Object)
                    country = addrEl.TryGetProperty("country", out var cEl) && cEl.ValueKind != JsonValueKind.Null ? cEl.GetString() : null;

                fetched.Add(new EventsAirContactDto(
                    ContactId: contactId,
                    FirstName: contact.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? "" : "",
                    LastName: contact.TryGetProperty("lastName", out var ln) ? ln.GetString() ?? "" : "",
                    Title: contact.TryGetProperty("title", out var t) ? t.GetString() : null,
                    JobTitle: contact.TryGetProperty("jobTitle", out var jt) ? jt.GetString() : null,
                    OrganizationName: contact.TryGetProperty("organizationName", out var org) ? org.GetString() : null,
                    PrimaryEmail: contact.TryGetProperty("primaryEmail", out var em) ? em.GetString() : null,
                    RegistrationTypeId: "",
                    RegistrationTypeName: "",
                    Country: country,
                    PhotoUrl: null,
                    RankValue: null, // Will need separate fetch
                    VehicleTypeValue: null
                ));
            }

            Console.WriteLine($"[SYNC] Light page offset={offset}: {pageCount} contacts (total: {fetched.Count})");
            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        // Fetch Rank and VehicleType values separately for the light path
        if (fetched.Count > 0)
        {
            Console.WriteLine($"[SYNC] Light path: fetching Rank and VehicleType values for {fetched.Count} contacts...");
            var contactIds = fetched.Select(c => c.ContactId).ToList();
            var rankValues = await FetchCustomFieldValuesAsync(baseUrl, eventCode, accessToken, rankFieldGuid, contactIds, httpClientFactory, cancellationToken);
            var vehicleTypeValues = await FetchCustomFieldValuesAsync(baseUrl, eventCode, accessToken, vehicleTypeFieldGuid, contactIds, httpClientFactory, cancellationToken);
            for (int i = 0; i < fetched.Count; i++)
            {
                var id = fetched[i].ContactId;
                fetched[i] = fetched[i] with
                {
                    RankValue = rankValues.TryGetValue(id, out var rank) ? rank : fetched[i].RankValue,
                    VehicleTypeValue = vehicleTypeValues.TryGetValue(id, out var vt) ? vt : fetched[i].VehicleTypeValue
                };
            }
        }

        return fetched;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper: Fetch custom field values per-contact (fallback only)
    // ═══════════════════════════════════════════════════════════════════════════
    private static async Task<Dictionary<string, string>> FetchCustomFieldValuesAsync(string baseUrl, string eventCode, string accessToken, string fieldDefinitionId, IEnumerable<string> contactIds, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var result = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allContactIds = contactIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var client = httpClientFactory.CreateClient();
        const int concurrency = 15;
        for (int i = 0; i < allContactIds.Count; i += concurrency)
        {
            var batch = allContactIds.Skip(i).Take(concurrency).ToList();
            var tasks = batch.Select(async contactId => {
                try {
                    var queryBody = JsonSerializer.Serialize(new { query = $"{{ event(id: \"{eventCode}\") {{ contact(id: \"{contactId}\") {{ id customFields {{ definitionId value }} }} }} }}" });
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql") { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) }, Content = new StringContent(queryBody, Encoding.UTF8, "application/json") };
                    var response = await client.SendAsync(req, cancellationToken);
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode) return;
                    var doc = JsonSerializer.Deserialize<JsonElement>(json);
                    if (doc.TryGetProperty("errors", out _) || !doc.TryGetProperty("data", out var data)) return;
                    var contactEl = data.GetProperty("event").GetProperty("contact");
                    if (contactEl.ValueKind == JsonValueKind.Null) return;
                    foreach (var cf in contactEl.GetProperty("customFields").EnumerateArray()) {
                        var defId = cf.GetProperty("definitionId").GetString() ?? "";
                        if (!string.Equals(defId, fieldDefinitionId, StringComparison.OrdinalIgnoreCase)) continue;
                        if (cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null) {
                            var val = v.ValueKind == JsonValueKind.Object ? (v.TryGetProperty("value", out var nv) ? nv.GetString() : v.TryGetProperty("text", out var tv) ? tv.GetString() : v.GetRawText()) : v.GetRawText();
                            if (!string.IsNullOrEmpty(val)) result[contactId] = val.Trim('"');
                        }
                    }
                } catch { }
            });
            await Task.WhenAll(tasks);
        }
        return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper: Fetch travel bookings (unchanged)
    // ═══════════════════════════════════════════════════════════════════════════
    private static async Task<List<EventsAirTravelDto>> FetchTravelBookingsFromEventsAirAsync(string baseUrl, string eventCode, string accessToken, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var result = new List<EventsAirTravelDto>();
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        const int pageSize = 100;
        int offset = 0;
        bool hasMore = true;

        while (hasMore)
        {
            var queryBody = JsonSerializer.Serialize(new { query = $"{{ event(id: \"{eventCode}\") {{ travelBookings(input: {{}}, limit: {pageSize}, offset: {offset}) {{ id contact {{ id }} travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} class bookingNotes comment }} }} }}" });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };
            var response = await client.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[TRAVEL-SYNC] Page offset={offset}: status={response.StatusCode}, responseLength={json.Length}");
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[TRAVEL-SYNC] Error response: {json[..Math.Min(json.Length, 1000)]}");
                break;
            }
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                Console.WriteLine($"[TRAVEL-SYNC] GraphQL errors: {errors}");
                break;
            }
            int pageCount = 0;
            if (doc.TryGetProperty("data", out var data) && data.TryGetProperty("event", out var eventObj) && eventObj.TryGetProperty("travelBookings", out var bookings))
            {
                foreach (var booking in bookings.EnumerateArray())
                {
                    var dto = new EventsAirTravelDto
                    {
                        Id = booking.GetProperty("id").GetString() ?? string.Empty,
                        ContactId = booking.TryGetProperty("contact", out var contact) && contact.ValueKind == JsonValueKind.Object ? (contact.TryGetProperty("id", out var cid) ? cid.GetString() ?? string.Empty : string.Empty) : string.Empty,
                        TravelTypeName = booking.TryGetProperty("travelType", out var tt) && tt.ValueKind == JsonValueKind.Object ? (tt.TryGetProperty("name", out var ttn) ? ttn.GetString() : null) : null,
                        FlightNumber = booking.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetString() : null,
                        CarrierName = booking.TryGetProperty("carrier", out var cr) && cr.ValueKind == JsonValueKind.Object ? (cr.TryGetProperty("name", out var crn) ? crn.GetString() : null) : null,
                        ArrivalDate = booking.TryGetProperty("arrivalDate", out var ad) && ad.ValueKind != JsonValueKind.Null ? ad.GetString() : null,
                        DepartureDate = booking.TryGetProperty("departureDate", out var dd) && dd.ValueKind != JsonValueKind.Null ? dd.GetString() : null,
                        Eta = booking.TryGetProperty("eta", out var eta) && eta.ValueKind != JsonValueKind.Null ? eta.GetString() : null,
                        Etd = booking.TryGetProperty("etd", out var etd) && etd.ValueKind != JsonValueKind.Null ? etd.GetString() : null,
                        DeparturePortName = booking.TryGetProperty("departurePort", out var dp) && dp.ValueKind == JsonValueKind.Object ? (dp.TryGetProperty("name", out var dpn) ? dpn.GetString() : null) : null,
                        DeparturePortCode = null,
                        ArrivalPortName = booking.TryGetProperty("arrivalPort", out var ap) && ap.ValueKind == JsonValueKind.Object ? (ap.TryGetProperty("name", out var apn) ? apn.GetString() : null) : null,
                        ArrivalPortCode = null,
                        Terminal = null,
                        SeatClass = booking.TryGetProperty("class", out var sc) && sc.ValueKind != JsonValueKind.Null ? sc.GetString() : null,
                        BookingNotes = booking.TryGetProperty("bookingNotes", out var bn) && bn.ValueKind != JsonValueKind.Null ? bn.GetString() : null,
                        Comment = booking.TryGetProperty("comment", out var cmt) && cmt.ValueKind != JsonValueKind.Null ? cmt.GetString() : null
                    };
                    result.Add(dto);
                    pageCount++;
                }
            }
            Console.WriteLine($"[TRAVEL-SYNC] Page offset={offset}: fetched {pageCount} bookings (total so far: {result.Count})");
            hasMore = pageCount >= pageSize;
            offset += pageSize;
        }
        Console.WriteLine($"[TRAVEL-SYNC] Completed: total {result.Count} travel bookings fetched");
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DTOs and helper records
    // ═══════════════════════════════════════════════════════════════════════════
    private record EventsAirContactDto(
        string ContactId, string FirstName, string LastName, string? Title,
        string? JobTitle, string? OrganizationName, string? PrimaryEmail,
        string RegistrationTypeId, string RegistrationTypeName,
        string? Country = null, string? PhotoUrl = null, string? RankValue = null, string? VehicleTypeValue = null);

    private record EventsAirRegistrationRaw(string ContactId, string FirstName, string LastName, string? Title, string? JobTitle, string? OrganizationName, string? PrimaryEmail, string RegistrationTypeId, string RegistrationTypeName, string? Country = null, string? PhotoUrl = null);
    private class SyncJobStatus { public string JobId { get; set; } = string.Empty; public string State { get; set; } = "pending"; public string Message { get; set; } = string.Empty; public int Added { get; set; } public int Updated { get; set; } public int Deactivated { get; set; } public int TotalFetched { get; set; } public DateTime StartedAt { get; set; } public DateTime? FinishedAt { get; set; }
        // Travel sync diagnostics
        public int TravelFetched { get; set; } public int TravelSavedNew { get; set; } public int TravelUpdated { get; set; } public int TravelRebooked { get; set; }
        public int TravelSkippedNoFlight { get; set; } public int TravelSkippedNoContact { get; set; } public int TravelSkippedNoGuest { get; set; }
        public int TravelErrors { get; set; } public string? TravelFirstError { get; set; }
        public int ConflictAlertCount { get; set; }
        // Vehicle type matching diagnostics
        public int VehicleTypeMatched { get; set; } public int VehicleTypeUnmatched { get; set; } public List<string> VehicleTypeUnmatchedValues { get; set; } = new(); }
    private class FieldFilter { public string FieldGuid { get; set; } = string.Empty; public List<string> SelectedValues { get; set; } = new(); }

    // ═══════════════════════════════════════════════════════════════════════════
    // JOURNEY STATUS FLOW ENDPOINTS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Get full journey status (inbound + outbound + history) for a guest.</summary>
    [HttpGet("{id:guid}/journey-status")]
    public async Task<IActionResult> GetJourneyStatus(Guid id, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var guest = await db.Guests
            .Include(g => g.StatusHistory.Where(h => !h.IsRolledBack).OrderBy(h => h.CreatedAt))
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        var activeAssignment = guest.VehicleAssignments.FirstOrDefault(va => va.IsActive);

        return Ok(new
        {
            inboundStatus = guest.InboundStatus,
            inboundStatusLabel = GetInboundLabel(guest.InboundStatus),
            receivedByEmbassyTeam = guest.ReceivedByEmbassyTeam,
            vehicleAssigned = activeAssignment != null,
            outboundStatus = guest.OutboundStatus,
            outboundStatusLabel = guest.OutboundStatus.HasValue ? GetOutboundLabel(guest.OutboundStatus.Value) : null,
            outboundUnlocked = guest.InboundStatus == InboundStatus.AtHotel,
            history = guest.StatusHistory
                .OrderBy(h => h.CreatedAt)
                .Select(h => new
                {
                    h.Id,
                    h.Track,
                    h.StatusValue,
                    h.StatusLabel,
                    h.ChangedByName,
                    h.ChangedByRole,
                    h.IsSystemGenerated,
                    h.Notes,
                    h.IsRolledBack,
                    h.CreatedAt
                })
        });
    }

    /// <summary>Set inbound status for a guest. Role-validated.</summary>
    [HttpPost("{id:guid}/inbound-status")]
    public async Task<IActionResult> SetInboundStatus(
        Guid id,
        [FromBody] SetStatusRequest req,
        [FromServices] AppDbContext db,
        [FromServices] IsDB.Hospitality.API.Services.NotificationTemplateService templateSvc,
        CancellationToken ct)
    {
        var guest = await db.Guests
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive))
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        var callerRole = GetCallerRole();
        var isAdmin = callerRole == UserRole.Admin;

        // Role permission check
        var allowed = req.Status switch
        {
            InboundStatus.Arrived => callerRole == UserRole.Airport || isAdmin,
            InboundStatus.ReceivedByEmbassyTeam => callerRole == UserRole.Airport || isAdmin,
            InboundStatus.AtHotel => callerRole == UserRole.Hotel || isAdmin,
            _ => isAdmin
        };
        if (!allowed) return Forbid();

        // Progression rules
        if (req.Status == InboundStatus.ReceivedByEmbassyTeam || req.Status == InboundStatus.AtHotel)
        {
            // Both require "Arrived" as a prerequisite
            if (guest.InboundStatus == InboundStatus.ArrivalScheduled)
                return BadRequest(new { message = "Guest must be marked as 'Arrived' first." });
        }

        if (req.Status == InboundStatus.AtHotel)
        {
            var vehicleAssigned = guest.VehicleAssignments.Any(va => va.IsActive);
            if (!guest.ReceivedByEmbassyTeam && !vehicleAssigned)
                return BadRequest(new { message = "At least one of 'Received by Embassy Team' or 'Vehicle Assigned' must be completed first." });
        }

        // Apply the status
        if (req.Status == InboundStatus.ReceivedByEmbassyTeam)
        {
            guest.ReceivedByEmbassyTeam = true;
            // Don't change InboundStatus enum — this is an independent flag
            // But if current status is still ArrivalScheduled, advance to Arrived first (shouldn't happen due to check above)
        }
        else
        {
            guest.InboundStatus = req.Status;
        }

        // Save hotel name and room number when checking in
        if (req.Status == InboundStatus.AtHotel)
        {
            if (!string.IsNullOrWhiteSpace(req.HotelName))
                guest.HotelName = req.HotelName.Trim();
            if (!string.IsNullOrWhiteSpace(req.RoomNumber))
                guest.RoomNumber = req.RoomNumber.Trim();
        }

        // Auto-unlock outbound when AtHotel
        if (req.Status == InboundStatus.AtHotel && !guest.OutboundStatus.HasValue)
        {
            guest.OutboundStatus = OutboundStatus.AtHotel;
            await AddHistoryEntry(db, guest.Id, StatusTrack.Outbound, (int)OutboundStatus.AtHotel,
                GetOutboundLabel(OutboundStatus.AtHotel), null, null, null, true, null);

            // Notify Hotel team that outbound is now active
            await CreateBellNotification(db, "Hotel",
                $"[Inbound] {guest.FirstName} {guest.LastName} has arrived at the hotel. Outbound journey tracking is now active.",
                AlertSeverity.High, ct);
        }

        // Log history
        var statusLabel = req.Status == InboundStatus.ReceivedByEmbassyTeam
            ? "Received by Embassy Team"
            : GetInboundLabel(req.Status);
        await AddHistoryEntry(db, guest.Id, StatusTrack.Inbound,
            req.Status == InboundStatus.ReceivedByEmbassyTeam ? (int)InboundStatus.ReceivedByEmbassyTeam : (int)req.Status,
            statusLabel, CurrentUserId, GetCallerName(), callerRole, false, req.Notes);

        await db.SaveChangesAsync(ct);

        // Send notifications (adds Notification rows, then saves)
        await SendStatusNotifications(db, guest, StatusTrack.Inbound, req.Status, callerRole, ct, templateSvc);
        await db.SaveChangesAsync(ct);

        return Ok(new { message = $"Inbound status updated to '{statusLabel}'." });
    }

    /// <summary>Undo the last inbound status change (if allowed by role and no subsequent changes).</summary>
    [HttpPost("{id:guid}/inbound-status/undo")]
    public async Task<IActionResult> UndoInboundStatus(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] IsDB.Hospitality.API.Services.NotificationTemplateService templateSvc,
        CancellationToken ct)
    {
        var guest = await db.Guests
            .Include(g => g.StatusHistory.OrderByDescending(h => h.CreatedAt))
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        var callerRole = GetCallerRole();
        var isAdmin = callerRole == UserRole.Admin;

        // Find the last non-rolled-back inbound entry
        var lastEntry = guest.StatusHistory
            .Where(h => h.Track == StatusTrack.Inbound && !h.IsRolledBack)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefault();

        if (lastEntry == null)
            return BadRequest(new { message = "No inbound status to undo." });

        // Check if a subsequent status was set by someone else (blocks rollback)
        var subsequentEntry = guest.StatusHistory
            .Where(h => h.Track == StatusTrack.Inbound && !h.IsRolledBack && h.CreatedAt > lastEntry.CreatedAt)
            .Any();
        if (subsequentEntry && !isAdmin)
            return BadRequest(new { message = "Cannot undo: a subsequent status has already been set." });

        // Only the same role (or Admin) can undo
        if (!isAdmin && lastEntry.ChangedByRole != callerRole)
            return Forbid();

        // Mark as rolled back
        lastEntry.IsRolledBack = true;
        lastEntry.UpdatedAt = DateTime.UtcNow;

        // Revert the guest status
        if (lastEntry.StatusValue == (int)InboundStatus.ReceivedByEmbassyTeam)
        {
            guest.ReceivedByEmbassyTeam = false;
        }
        else
        {
            // Find the previous status
            var previousEntry = guest.StatusHistory
                .Where(h => h.Track == StatusTrack.Inbound && !h.IsRolledBack && h.CreatedAt < lastEntry.CreatedAt)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();
            guest.InboundStatus = previousEntry != null
                ? (InboundStatus)previousEntry.StatusValue
                : InboundStatus.ArrivalScheduled;
        }

        await db.SaveChangesAsync(ct);
        await SendUndoInboundNotification(db, guest, lastEntry.StatusValue, ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Inbound status rolled back." });
    }

    /// <summary>Set outbound status for a guest. Role-validated. Requires inbound AtHotel.</summary>
    [HttpPost("{id:guid}/outbound-status")]
    public async Task<IActionResult> SetOutboundStatus(
        Guid id,
        [FromBody] SetOutboundStatusRequest req,
        [FromServices] AppDbContext db,
        [FromServices] IsDB.Hospitality.API.Services.NotificationTemplateService templateSvc,
        CancellationToken ct)
    {
        var guest = await db.Guests.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        if (guest.InboundStatus != InboundStatus.AtHotel)
            return BadRequest(new { message = "Outbound tracking is only available after the guest has arrived at the hotel." });

        var callerRole = GetCallerRole();
        var isAdmin = callerRole == UserRole.Admin;

        var allowed = req.Status switch
        {
            OutboundStatus.InTransferToAirport => callerRole == UserRole.Hotel || isAdmin,
            OutboundStatus.AtAirport => callerRole == UserRole.Airport || callerRole == UserRole.Transport || isAdmin,
            OutboundStatus.BoardingCompleted => callerRole == UserRole.Airport || isAdmin,
            _ => isAdmin
        };
        if (!allowed) return Forbid();

        // Sequential progression check
        var currentOutbound = guest.OutboundStatus ?? OutboundStatus.AtHotel;
        if ((int)req.Status != (int)currentOutbound + 1 && !isAdmin)
            return BadRequest(new { message = "Outbound status must progress sequentially." });

        guest.OutboundStatus = req.Status;

        await AddHistoryEntry(db, guest.Id, StatusTrack.Outbound, (int)req.Status,
            GetOutboundLabel(req.Status), CurrentUserId, GetCallerName(), callerRole, false, req.Notes);

        await db.SaveChangesAsync(ct);

        await SendOutboundNotifications(db, guest, req.Status, callerRole, ct, templateSvc);
        await db.SaveChangesAsync(ct);

        return Ok(new { message = $"Outbound status updated to '{GetOutboundLabel(req.Status)}'." });
    }

    /// <summary>Undo the last outbound status change.</summary>
    [HttpPost("{id:guid}/outbound-status/undo")]
    public async Task<IActionResult> UndoOutboundStatus(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] IsDB.Hospitality.API.Services.NotificationTemplateService templateSvc,
        CancellationToken ct)
    {
        var guest = await db.Guests
            .Include(g => g.StatusHistory.OrderByDescending(h => h.CreatedAt))
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        var callerRole = GetCallerRole();
        var isAdmin = callerRole == UserRole.Admin;

        var lastEntry = guest.StatusHistory
            .Where(h => h.Track == StatusTrack.Outbound && !h.IsRolledBack)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefault();

        if (lastEntry == null || lastEntry.StatusValue == (int)OutboundStatus.AtHotel)
            return BadRequest(new { message = "No outbound status to undo." });

        var subsequentEntry = guest.StatusHistory
            .Where(h => h.Track == StatusTrack.Outbound && !h.IsRolledBack && h.CreatedAt > lastEntry.CreatedAt)
            .Any();
        if (subsequentEntry && !isAdmin)
            return BadRequest(new { message = "Cannot undo: a subsequent status has already been set." });

        if (!isAdmin && lastEntry.ChangedByRole != callerRole)
            return Forbid();

        lastEntry.IsRolledBack = true;
        lastEntry.UpdatedAt = DateTime.UtcNow;

        var previousEntry = guest.StatusHistory
            .Where(h => h.Track == StatusTrack.Outbound && !h.IsRolledBack && h.CreatedAt < lastEntry.CreatedAt)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefault();
        guest.OutboundStatus = previousEntry != null
            ? (OutboundStatus)previousEntry.StatusValue
            : OutboundStatus.AtHotel;

        await db.SaveChangesAsync(ct);
        await SendUndoOutboundNotification(db, guest, lastEntry.StatusValue, ct);
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Outbound status rolled back." });
    }

    /// <summary>Admin-only: force any inbound or outbound status.</summary>
    [HttpPost("{id:guid}/status/force")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ForceStatus(
        Guid id,
        [FromBody] ForceStatusRequest req,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var guest = await db.Guests.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        if (req.Track == StatusTrack.Inbound)
        {
            if (!Enum.IsDefined(typeof(InboundStatus), req.StatusValue))
                return BadRequest(new { message = "Invalid inbound status value." });
            var status = (InboundStatus)req.StatusValue;
            if (status == InboundStatus.ReceivedByEmbassyTeam)
                guest.ReceivedByEmbassyTeam = true;
            else
                guest.InboundStatus = status;

            if (status == InboundStatus.AtHotel && !guest.OutboundStatus.HasValue)
            {
                guest.OutboundStatus = OutboundStatus.AtHotel;
                await AddHistoryEntry(db, guest.Id, StatusTrack.Outbound, (int)OutboundStatus.AtHotel,
                    GetOutboundLabel(OutboundStatus.AtHotel), CurrentUserId, GetCallerName(), UserRole.Admin, false, "Auto-set by Admin force");
            }
        }
        else
        {
            if (!Enum.IsDefined(typeof(OutboundStatus), req.StatusValue))
                return BadRequest(new { message = "Invalid outbound status value." });
            guest.OutboundStatus = (OutboundStatus)req.StatusValue;
        }

        await AddHistoryEntry(db, guest.Id, req.Track, req.StatusValue,
            req.Track == StatusTrack.Inbound ? GetInboundLabel((InboundStatus)req.StatusValue) : GetOutboundLabel((OutboundStatus)req.StatusValue),
            CurrentUserId, GetCallerName(), UserRole.Admin, false, req.Notes ?? "Force-set by Admin");

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Status force-set successfully." });
    }

    /// <summary>Admin-only: reset all journey statuses for a guest.</summary>
    [HttpPost("{id:guid}/status/reset")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetStatus(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var guest = await db.Guests
            .Include(g => g.StatusHistory)
            .FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest == null) return NotFound();

        guest.InboundStatus = InboundStatus.ArrivalScheduled;
        guest.ReceivedByEmbassyTeam = false;
        guest.OutboundStatus = null;

        foreach (var h in guest.StatusHistory)
            h.IsRolledBack = true;

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Journey status reset to initial state." });
    }

    // ─── Private helpers ────────────────────────────────────────────────────────

    private UserRole GetCallerRole()
    {
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                     ?? User.FindFirst("role")?.Value
                     ?? "Admin";
        return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Admin;
    }

    private string GetCallerName()
        => User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";

    private static string GetInboundLabel(InboundStatus status) => status switch
    {
        InboundStatus.ArrivalScheduled => "Arrival Scheduled",
        InboundStatus.Arrived => "Arrived",
        InboundStatus.ReceivedByEmbassyTeam => "Received by Embassy Team",
        InboundStatus.VehicleAssigned => "Vehicle Assigned",
        InboundStatus.AtHotel => "At Hotel",
        _ => status.ToString()
    };

    private static string GetOutboundLabel(OutboundStatus status) => status switch
    {
        OutboundStatus.AtHotel => "At Hotel",
        OutboundStatus.InTransferToAirport => "In Transfer to Airport",
        OutboundStatus.AtAirport => "At Airport",
        OutboundStatus.BoardingCompleted => "Boarding Completed",
        _ => status.ToString()
    };

    private static async Task AddHistoryEntry(
        AppDbContext db, Guid guestId, StatusTrack track, int statusValue, string statusLabel,
        Guid? changedByStaffId, string? changedByName, UserRole? changedByRole,
        bool isSystemGenerated, string? notes)
    {
        var entry = new GuestStatusHistory
        {
            Id = Guid.NewGuid(),
            GuestId = guestId,
            Track = track,
            StatusValue = statusValue,
            StatusLabel = statusLabel,
            ChangedByStaffId = changedByStaffId == Guid.Empty ? null : changedByStaffId,
            ChangedByName = changedByName,
            ChangedByRole = changedByRole,
            IsSystemGenerated = isSystemGenerated,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await db.GuestStatusHistories.AddAsync(entry);
    }

    // ─── Notification helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a bell Notification targeting the given role(s), plus a separate Admin copy.
    /// If a templateSvc and eventKey are provided, the message and priority are resolved from
    /// the database template (allowing Admin to customize them). Falls back to hardcoded values.
    /// </summary>
    private async Task CreateBellNotification(
        AppDbContext db,
        string targetRoles,
        string message,
        AlertSeverity priority,
        CancellationToken ct,
        IsDB.Hospitality.API.Services.NotificationTemplateService? templateSvc = null,
        string? eventKey = null,
        string? guestName = null,
        bool suppressAdminCopy = false)
    {
        // Resolve from template if available
        if (templateSvc != null && eventKey != null)
        {
            var template = await templateSvc.GetTemplateAsync(eventKey);
            if (template != null)
            {
                message  = template.MessageTemplate.Replace("{GuestName}", guestName ?? "");
                priority = template.Priority;
                // TargetRoles is system-defined — not overridable by Admin
            }
        }

        // Notification for the target team
        db.Notifications.Add(new Notification
        {
            Message = message,
            TargetRoles = targetRoles,
            Priority = priority,
            CreatedByStaffId = CurrentUserId
        });

        // Explicit Admin copy — only when Admin is not already the target and not suppressed
        if (!suppressAdminCopy
            && !targetRoles.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            && !targetRoles.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            // Use admin-specific template if available
            string adminMessage  = message;
            AlertSeverity adminPriority = priority;
            if (templateSvc != null && eventKey != null)
            {
                var adminTemplate = await templateSvc.GetTemplateAsync(eventKey + ".admin_copy");
                if (adminTemplate != null)
                {
                    adminMessage  = adminTemplate.MessageTemplate.Replace("{GuestName}", guestName ?? "");
                    adminPriority = adminTemplate.Priority;
                }
            }
            db.Notifications.Add(new Notification
            {
                Message = adminMessage,
                TargetRoles = "Admin",
                Priority = adminPriority,
                CreatedByStaffId = CurrentUserId
            });
        }

        await Task.CompletedTask; // notifications are saved by the caller's SaveChangesAsync
    }

    // ─── Inbound status notifications ────────────────────────────────────────────

    private async Task SendStatusNotifications(
        AppDbContext db, Guest guest, StatusTrack track, InboundStatus status, UserRole callerRole, CancellationToken ct,
        IsDB.Hospitality.API.Services.NotificationTemplateService? templateSvc = null)
    {
        var name = $"{guest.FirstName} {guest.LastName}";

        switch (status)
        {
            case InboundStatus.Arrived:
                // Transport: Critical — guest arrived, vehicle dispatch needed
                await CreateBellNotification(db, "Transport",
                    $"[Inbound] {name} has arrived at the airport.",
                    AlertSeverity.Critical, ct, templateSvc, "inbound.arrived", name);
                // Hotel: High — guest arrived, prepare for check-in (no admin copy — already sent above)
                await CreateBellNotification(db, "Hotel",
                    $"[Inbound] {name} has arrived at the airport.",
                    AlertSeverity.High, ct, templateSvc, "inbound.arrived.hotel_copy", name, suppressAdminCopy: true);
                break;

            case InboundStatus.ReceivedByEmbassyTeam:
                // Transport: High — embassy handover complete
                await CreateBellNotification(db, "Transport",
                    $"[Inbound] {name} has been received by the Embassy team.",
                    AlertSeverity.High, ct, templateSvc, "inbound.received_by_embassy", name);
                // Hotel: High — guest received by embassy team (no admin copy — already sent above)
                await CreateBellNotification(db, "Hotel",
                    $"[Inbound] {name} has been received by the Embassy team.",
                    AlertSeverity.High, ct, templateSvc, "inbound.received_by_embassy", name, suppressAdminCopy: true);
                break;

            case InboundStatus.VehicleAssigned:
                // Hotel: Critical — vehicle dispatched, guest on the way (status auto-set path)
                await CreateBellNotification(db, "Hotel",
                    $"[Inbound] {name}'s vehicle was assigned (dispatched from Airport).",
                    AlertSeverity.Critical, ct, templateSvc, "inbound.vehicle_status_changed", name);
                break;

            case InboundStatus.AtHotel:
                // Admin: High — check-in confirmed
                await CreateBellNotification(db, "Admin",
                    $"[Inbound] {name} has checked in at the hotel.",
                    AlertSeverity.High, ct);
                break;
        }
    }

    // ─── Outbound status notifications ───────────────────────────────────────────

    private async Task SendOutboundNotifications(
        AppDbContext db, Guest guest, OutboundStatus status, UserRole callerRole, CancellationToken ct,
        IsDB.Hospitality.API.Services.NotificationTemplateService? templateSvc = null)
    {
        var name = $"{guest.FirstName} {guest.LastName}";

        switch (status)
        {
            case OutboundStatus.InTransferToAirport:
                await CreateBellNotification(db, "Transport",
                    $"[Outbound] {name} is in transfer to the airport — prepare for arrival.",
                    AlertSeverity.High, ct, templateSvc, "outbound.in_transfer", name);
                break;

            case OutboundStatus.AtAirport:
                await CreateBellNotification(db, "Airport",
                    $"[Outbound] {name} has arrived at the departure terminal.",
                    AlertSeverity.Critical, ct, templateSvc, "outbound.at_airport", name);
                break;

            case OutboundStatus.BoardingCompleted:
                await CreateBellNotification(db, "Admin",
                    $"[Outbound] {name} has completed boarding.",
                    AlertSeverity.Medium, ct);
                break;
        }
    }

    // ─── Undo notifications ───────────────────────────────────────────────────────

    private async Task SendUndoInboundNotification(
        AppDbContext db, Guest guest, int undoneStatusValue, CancellationToken ct)
    {
        var name = $"{guest.FirstName} {guest.LastName}";

        switch ((InboundStatus)undoneStatusValue)
        {
            case InboundStatus.Arrived:
                // Mirror of forward: Transport (Critical) + Hotel (High) were notified on Arrived
                await CreateBellNotification(db, "Transport",
                    $"[Undo] {name}'s 'Arrived' status has been reversed.",
                    AlertSeverity.High, ct);
                await CreateBellNotification(db, "Hotel",
                    $"[Undo] {name}'s 'Arrived' status has been reversed.",
                    AlertSeverity.High, ct);
                break;

            case InboundStatus.ReceivedByEmbassyTeam:
                await CreateBellNotification(db, "Transport",
                    $"[Undo] {name}'s 'Received by Embassy Team' status has been reversed.",
                    AlertSeverity.Medium, ct);
                await CreateBellNotification(db, "Hotel",
                    $"[Undo] {name}'s 'Received by Embassy Team' status has been reversed.",
                    AlertSeverity.Medium, ct);
                break;

            case InboundStatus.VehicleAssigned:
                // Mirror of forward: Hotel was notified on VehicleAssigned
                await CreateBellNotification(db, "Hotel",
                    $"[Undo] {name}'s 'Vehicle Assigned' status has been reversed.",
                    AlertSeverity.High, ct);
                break;

            case InboundStatus.AtHotel:
                await CreateBellNotification(db, "Admin",
                    $"[Undo] {name}'s 'At Hotel' check-in has been reversed.",
                    AlertSeverity.High, ct);
                break;
        }
    }

    // ─── Flight change acknowledgement ──────────────────────────────────────────

    /// <summary>
    /// Clears the ChangedSinceLastView flag on all travel bookings for a guest.
    /// Called automatically when the user opens the guest detail page.
    /// </summary>
    [HttpPost("{id:guid}/acknowledge-flight-changes")]
    [Authorize]
    public async Task<IActionResult> AcknowledgeFlightChanges(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var bookings = await db.TravelBookings
            .Where(tb => tb.GuestId == id && tb.ChangedSinceLastView)
            .ToListAsync(ct);

        if (!bookings.Any()) return Ok(new { acknowledged = 0 });

        foreach (var b in bookings)
            b.ChangedSinceLastView = false;

        await db.SaveChangesAsync(ct);
        return Ok(new { acknowledged = bookings.Count });
    }

    /// <summary>Update hotel name and/or room number for a guest already at hotel. Admin and Hotel roles only.</summary>
    [HttpPatch("{id:guid}/hotel-assignment")]
    public async Task<IActionResult> UpdateHotelAssignment(
        Guid id,
        [FromBody] UpdateHotelAssignmentRequest req,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var callerRole = GetCallerRole();
        var isAdmin = callerRole == UserRole.Admin;
        var isHotel = callerRole == UserRole.Hotel;
        if (!isAdmin && !isHotel)
            return Forbid();

        var guest = await db.Guests.FindAsync(new object[] { id }, ct);
        if (guest == null) return NotFound();

        if (guest.InboundStatus != InboundStatus.AtHotel)
            return BadRequest(new { message = "Hotel assignment can only be updated after the guest has arrived at the hotel." });

        // Build diff notes
        var parts = new List<string>();
        if (req.HotelName != null && req.HotelName != guest.HotelName)
            parts.Add($"Hotel: {guest.HotelName ?? "(none)"} \u2192 {req.HotelName}");
        if (req.RoomNumber != null && req.RoomNumber != guest.RoomNumber)
            parts.Add($"Room: {guest.RoomNumber ?? "(none)"} \u2192 {req.RoomNumber}");

        if (parts.Count == 0)
            return Ok(new { message = "No changes detected." });

        // Apply changes
        if (req.HotelName != null) guest.HotelName = string.IsNullOrWhiteSpace(req.HotelName) ? null : req.HotelName.Trim();
        if (req.RoomNumber != null) guest.RoomNumber = string.IsNullOrWhiteSpace(req.RoomNumber) ? null : req.RoomNumber.Trim();
        guest.UpdatedAt = DateTime.UtcNow;

        // Record history
        await AddHistoryEntry(db, guest.Id, StatusTrack.Inbound,
            (int)InboundStatus.AtHotel,
            "Hotel Assignment Updated",
            CurrentUserId, GetCallerName(), callerRole,
            false, string.Join(", ", parts));

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Hotel assignment updated.", notes = string.Join(", ", parts) });
    }

    private async Task SendUndoOutboundNotification(
        AppDbContext db, Guest guest, int undoneStatusValue, CancellationToken ct)
    {
        var name = $"{guest.FirstName} {guest.LastName}";

        switch ((OutboundStatus)undoneStatusValue)
        {
            case OutboundStatus.InTransferToAirport:
                await CreateBellNotification(db, "Transport",
                    $"[Undo] {name}'s 'In Transfer to Airport' status has been reversed.",
                    AlertSeverity.Medium, ct);
                break;

            case OutboundStatus.AtAirport:
                await CreateBellNotification(db, "Airport",
                    $"[Undo] {name}'s 'At Airport' status has been reversed.",
                    AlertSeverity.High, ct);
                break;

            case OutboundStatus.BoardingCompleted:
                await CreateBellNotification(db, "Admin",
                    $"[Undo] {name}'s 'Boarding Completed' status has been reversed.",
                    AlertSeverity.Medium, ct);
                break;
        }
    }

    // ─── Export Roster ────────────────────────────────────────────────────────
    // GET /api/guests/export
    // Admin-only: export filtered guest roster as CSV.
    // Query params:
    //   registrationTypes  – comma-separated list of RegistrationTypeName values
    //   ranks              – comma-separated list of RankValue values
    //   deservedCarClassIds – comma-separated list of CarClass GUIDs
    //   columns            – comma-separated list of column keys to include
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportRosterCsv(
        [FromServices] AppDbContext db,
        [FromQuery] string? registrationTypes = null,
        [FromQuery] string? ranks = null,
        [FromQuery] string? deservedCarClassIds = null,
        [FromQuery] string? columns = null,
        CancellationToken ct = default)
    {
        // Parse filter lists
        var regTypes = ParseList(registrationTypes);
        var rankList = ParseList(ranks);
        var classIds = ParseList(deservedCarClassIds)
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue).Select(g => g!.Value).ToList();

        // Parse requested columns (default: all)
        var allColumns = new[] {
            "title","name","rank","country","registrationType","deservedCarClass",
            "arrivalFlight","arrivalAirline","arrivalDateTime","arrivalRoute",
            "departureFlight","departureAirline","departureDatetime","departureRoute",
            "carNumber","driverName","driverPhone","assignedCarClass","assignmentType","hotelName","roomNumber"
        };
        var requestedCols = string.IsNullOrWhiteSpace(columns)
            ? new HashSet<string>(allColumns)
            : new HashSet<string>(ParseList(columns));

        // Build query with required joins
        var guests = await db.Guests
            .Where(g => g.IsActive)
            .Include(g => g.TravelBookings).ThenInclude(tb => tb.Flight)
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive)).ThenInclude(va => va.Vehicle).ThenInclude(v => v.Driver)
            .Include(g => g.VehicleAssignments.Where(va => va.IsActive)).ThenInclude(va => va.Vehicle).ThenInclude(v => v.CarClass)
            .Include(g => g.DeservedCarClass)
            .OrderBy(g => g.LastName).ThenBy(g => g.FirstName)
            .ToListAsync(ct);

        // Sentinel for null/empty values
        const string Unset = "__UNSET__";

        // Apply filters
        if (regTypes.Count > 0)
            guests = guests.Where(g => g.RegistrationTypeName != null && regTypes.Contains(g.RegistrationTypeName)).ToList();
        if (rankList.Count > 0)
        {
            var includeUnset = rankList.Contains(Unset);
            var realRanks    = rankList.Where(r => r != Unset).ToList();
            guests = guests.Where(g =>
                (includeUnset && string.IsNullOrWhiteSpace(g.RankValue)) ||
                (realRanks.Count > 0 && !string.IsNullOrWhiteSpace(g.RankValue) && realRanks.Contains(g.RankValue))
            ).ToList();
        }
        if (classIds.Count > 0 || (ParseList(deservedCarClassIds).Contains(Unset)))
        {
            var rawClassList  = ParseList(deservedCarClassIds);
            var includeUnset  = rawClassList.Contains(Unset);
            guests = guests.Where(g =>
                (includeUnset && !g.DeservedCarClassId.HasValue) ||
                (classIds.Count > 0 && g.DeservedCarClassId.HasValue && classIds.Contains(g.DeservedCarClassId.Value))
            ).ToList();
        }

        // Build CSV
        var sb = new StringBuilder();

        // Header row — only requested columns
        var headers = new List<string>();
        if (requestedCols.Contains("title"))           headers.Add("Title");
        if (requestedCols.Contains("name"))            headers.Add("Name");
        if (requestedCols.Contains("rank"))            headers.Add("Rank");
        if (requestedCols.Contains("country"))         headers.Add("Country");
        if (requestedCols.Contains("registrationType")) headers.Add("Registration Type");
        if (requestedCols.Contains("deservedCarClass")) headers.Add("Deserve Car Class");
        if (requestedCols.Contains("arrivalFlight"))   headers.Add("Arrival Flight No.");
        if (requestedCols.Contains("arrivalAirline"))  headers.Add("Arrival Airline");
        if (requestedCols.Contains("arrivalDateTime")) headers.Add("Arrival Date/Time");
        if (requestedCols.Contains("arrivalRoute"))    headers.Add("Arrival Route");
        if (requestedCols.Contains("departureFlight")) headers.Add("Departure Flight No.");
        if (requestedCols.Contains("departureAirline")) headers.Add("Departure Airline");
        if (requestedCols.Contains("departureDatetime")) headers.Add("Departure Date/Time");
        if (requestedCols.Contains("departureRoute"))  headers.Add("Departure Route");
        if (requestedCols.Contains("carNumber"))       headers.Add("Car Number");
        if (requestedCols.Contains("driverName"))      headers.Add("Driver Name");
        if (requestedCols.Contains("driverPhone"))     headers.Add("Driver Phone");
        if (requestedCols.Contains("assignedCarClass")) headers.Add("Assigned Car Class");
        if (requestedCols.Contains("assignmentType"))  headers.Add("Assignment Type");
        if (requestedCols.Contains("hotelName"))       headers.Add("Hotel Name");
        if (requestedCols.Contains("roomNumber"))      headers.Add("Room Number");
        sb.AppendLine(string.Join(",", headers));

        // Data rows
        foreach (var g in guests)
        {
            var arrivalBooking  = g.TravelBookings.FirstOrDefault(tb => tb.IsArrival);
            var departureBooking = g.TravelBookings.FirstOrDefault(tb => !tb.IsArrival);
            var activeAssignment = g.VehicleAssignments.FirstOrDefault(va => va.IsActive);
            var vehicle = activeAssignment?.Vehicle;
            var driver  = vehicle?.Driver;

            var row = new List<string>();
            if (requestedCols.Contains("title"))           row.Add(EscapeRosterCsv(g.Title ?? ""));
            if (requestedCols.Contains("name"))            row.Add(EscapeRosterCsv($"{g.FirstName} {g.LastName}".Trim()));
            if (requestedCols.Contains("rank"))            row.Add(EscapeRosterCsv(g.RankValue ?? ""));
            if (requestedCols.Contains("country"))         row.Add(EscapeRosterCsv(g.Country ?? ""));
            if (requestedCols.Contains("registrationType")) row.Add(EscapeRosterCsv(g.RegistrationTypeName ?? ""));
            if (requestedCols.Contains("deservedCarClass")) row.Add(EscapeRosterCsv(g.DeservedCarClass?.Name ?? ""));
            if (requestedCols.Contains("arrivalFlight"))   row.Add(EscapeRosterCsv(arrivalBooking?.Flight?.FlightNumber ?? ""));
            if (requestedCols.Contains("arrivalAirline"))  row.Add(EscapeRosterCsv(arrivalBooking?.Flight?.AirlineName ?? ""));
            if (requestedCols.Contains("arrivalDateTime")) row.Add(EscapeRosterCsv(arrivalBooking?.Flight?.ScheduledArrival.ToString("yyyy-MM-dd HH:mm") ?? ""));
            if (requestedCols.Contains("arrivalRoute"))    row.Add(EscapeRosterCsv($"{arrivalBooking?.Flight?.DeparturePortIataCode ?? ""} → {arrivalBooking?.Flight?.ArrivalPortIataCode ?? ""}".Trim(' ', '→', ' ')));
            if (requestedCols.Contains("departureFlight")) row.Add(EscapeRosterCsv(departureBooking?.Flight?.FlightNumber ?? ""));
            if (requestedCols.Contains("departureAirline")) row.Add(EscapeRosterCsv(departureBooking?.Flight?.AirlineName ?? ""));
            if (requestedCols.Contains("departureDatetime")) row.Add(EscapeRosterCsv(departureBooking?.Flight?.ScheduledDeparture.ToString("yyyy-MM-dd HH:mm") ?? ""));
            if (requestedCols.Contains("departureRoute"))  row.Add(EscapeRosterCsv($"{departureBooking?.Flight?.DeparturePortIataCode ?? ""} → {departureBooking?.Flight?.ArrivalPortIataCode ?? ""}".Trim(' ', '→', ' ')));
            if (requestedCols.Contains("carNumber"))       row.Add(EscapeRosterCsv(vehicle?.CarNumber ?? ""));
            if (requestedCols.Contains("driverName"))      row.Add(EscapeRosterCsv(driver?.FullName ?? vehicle?.DriverName ?? ""));
            if (requestedCols.Contains("driverPhone"))     row.Add(EscapeRosterCsv(driver?.Phone ?? vehicle?.DriverPhone ?? ""));
            if (requestedCols.Contains("assignedCarClass")) row.Add(EscapeRosterCsv(vehicle?.CarClass?.Name ?? ""));
            if (requestedCols.Contains("assignmentType"))  row.Add(EscapeRosterCsv(activeAssignment != null ? activeAssignment.AssignmentType.ToString() : ""));
            if (requestedCols.Contains("hotelName"))       row.Add(EscapeRosterCsv(g.HotelName ?? ""));
            if (requestedCols.Contains("roomNumber"))      row.Add(EscapeRosterCsv(g.RoomNumber ?? ""));
            sb.AppendLine(string.Join(",", row));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"roster_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    private static List<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new List<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
    }

    private static string EscapeRosterCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{ value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// Admin-only: directly update the scheduled arrival of a travel booking's linked flight.
    /// Intended for testing the AviationStack sync against today's live data.
    /// </summary>
    [HttpPatch("{guestId:guid}/travel-bookings/{bookingId:guid}/scheduled-arrival")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PatchTravelBookingScheduledArrival(
        Guid guestId, Guid bookingId,
        [FromBody] PatchScheduledArrivalRequest req,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var booking = await db.TravelBookings
            .Include(tb => tb.Flight)
            .FirstOrDefaultAsync(tb => tb.Id == bookingId && tb.GuestId == guestId, ct);

        if (booking == null)
            return NotFound(new { message = "Travel booking not found" });

        if (booking.Flight == null)
            return BadRequest(new { message = "Booking has no linked flight" });

        var oldDate = booking.Flight.ScheduledArrival;
        booking.Flight.ScheduledArrival = req.ScheduledArrival.ToUniversalTime();
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            bookingId,
            flightId = booking.Flight.Id,
            flightNumber = booking.Flight.FlightNumber,
            oldScheduledArrival = oldDate,
            newScheduledArrival = booking.Flight.ScheduledArrival
        });
    }
}
public record UpdateStatusRequest(GuestStatus Status, string? Notes = null);
public record CompleteChecklistRequest(string? Notes = null);
public record BulkAssignCarClassRequest(List<Guid> GuestIds, Guid? CarClassId);
public record SetStatusRequest(InboundStatus Status, string? Notes = null, string? HotelName = null, string? RoomNumber = null);
public record SetOutboundStatusRequest(OutboundStatus Status, string? Notes = null);
public record ForceStatusRequest(StatusTrack Track, int StatusValue, string? Notes = null);
public record UpdateHotelAssignmentRequest(string? HotelName, string? RoomNumber);
public record PatchScheduledArrivalRequest(DateTime ScheduledArrival);
