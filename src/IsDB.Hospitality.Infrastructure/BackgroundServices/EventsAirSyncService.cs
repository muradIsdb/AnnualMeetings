using IsDB.Hospitality.Application.Common.Models;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IsDB.Hospitality.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that automatically syncs guests from EventsAir.
/// Reads credentials and configuration from the EventsAirConfigs database table.
/// Respects AutoSyncEnabled and SyncIntervalMinutes settings.
/// Uses the same 3-pass logic as the manual sync endpoint.
/// </summary>
public class EventsAirSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventsAirSyncService> _logger;
    private readonly ISystemLogService _systemLogService;

    // Default interval used only before the first DB read
    private TimeSpan _syncInterval = TimeSpan.FromMinutes(15);

    public EventsAirSyncService(IServiceProvider serviceProvider, ILogger<EventsAirSyncService> logger, ISystemLogService systemLogService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _systemLogService = systemLogService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventsAir background sync service started.");

        // ── Startup sync ──────────────────────────────────────────────────────
        // Wait briefly for the app to fully initialise, then check SyncOnStartup.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        if (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var startupConfig = await db.EventsAirConfigs.FirstOrDefaultAsync(stoppingToken);

                if (startupConfig is { IsActive: true, SyncOnStartup: true, AutoSyncEnabled: true } &&
                    !string.IsNullOrWhiteSpace(startupConfig.ClientId) &&
                    !string.IsNullOrWhiteSpace(startupConfig.ClientSecret) &&
                    !string.IsNullOrWhiteSpace(startupConfig.EventCode))
                {
                    _logger.LogInformation("EventsAir SyncOnStartup=true — running initial sync.");
                    await SyncAsync(stoppingToken, isStartupSync: true);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventsAir startup sync failed.");
                await _systemLogService.LogAsync(LogSeverity.Error, "EventsAirSync", "Startup sync failed", ex.ToString());
            }
        }

        // ── Recurring sync loop ───────────────────────────────────────────────
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read interval from DB BEFORE delaying, so it updates even if startup sync failed
                using (var scope = _serviceProvider.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var config = await db.EventsAirConfigs.FirstOrDefaultAsync(stoppingToken);
                    if (config != null && config.SyncIntervalMinutes > 0)
                    {
                        _syncInterval = TimeSpan.FromMinutes(config.SyncIntervalMinutes);
                    }
                }
                
                await Task.Delay(_syncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in EventsAir background sync loop.");
                await _systemLogService.LogAsync(LogSeverity.Critical, "EventsAirSync", "Unhandled error in background loop", ex.ToString());
            }
        }

        _logger.LogInformation("EventsAir background sync service stopped.");
    }

    private async Task SyncAsync(CancellationToken cancellationToken, bool isStartupSync = false)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // ── Read config from DB ───────────────────────────────────────────────
        var config = await db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            _logger.LogDebug("EventsAir background sync skipped: no configuration found in database.");
            return;
        }

        // Interval is now read at the top of the recurring loop, no need to update here

        if (!config.IsActive || !config.AutoSyncEnabled)
        {
            _logger.LogDebug("EventsAir background sync skipped: AutoSyncEnabled={AutoSync}, IsActive={IsActive}.",
                config.AutoSyncEnabled, config.IsActive);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret) ||
            string.IsNullOrWhiteSpace(config.EventCode) || string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            _logger.LogWarning("EventsAir background sync skipped: credentials or EventCode not configured.");
            await _systemLogService.LogAsync(LogSeverity.Warning, "EventsAirSync", "Sync skipped: credentials or EventCode not configured");
            return;
        }

        _logger.LogInformation("EventsAir background sync starting (EventCode={EventCode}).", config.EventCode);

        var sw = Stopwatch.StartNew();
        var clientId = config.ClientId;
        var clientSecret = config.ClientSecret;
        var eventCode = config.EventCode;
        var apiBaseUrl = config.ApiBaseUrl;

        // ── Acquire token ─────────────────────────────────────────────────────
        string token;
        try
        {
            // OAuthScope is NotMapped on EventsAirConfig entity — read via raw SQL
            string oAuthScope;
            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(cancellationToken);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT \"OAuthScope\" FROM \"EventsAirConfigs\" LIMIT 1";
                var scopeResult = await cmd.ExecuteScalarAsync(cancellationToken);
                oAuthScope = scopeResult is string s && !string.IsNullOrWhiteSpace(s)
                    ? s
                    : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
            }
            catch
            {
                oAuthScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
            }
            token = await EventsAirSyncHelpers.GetEventsAirTokenAsync(clientId, clientSecret, httpClientFactory, oAuthScope);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "EventsAir background sync failed: could not acquire token.");
            await _systemLogService.LogAsync(LogSeverity.Error, "EventsAirSync", "Failed to acquire token", ex.ToString());
            config.LastSyncAt = DateTime.UtcNow;
            config.LastSyncStatus = "Failed";
            config.LastSyncMessage = $"Token acquisition failed: {ex.Message}";
            await db.SaveChangesAsync(CancellationToken.None);
            await WriteSyncLogAsync(db, "Failed", config.LastSyncMessage, 0, 0, (int)sw.ElapsedMilliseconds,
                triggerSource: isStartupSync ? "Startup" : "System Auto-Sync", syncType: "Scheduled");
            return;
        }

        int added = 0, updated = 0, deactivated = 0, travelSynced = 0;

        try
        {
            // ══════════════════════════════════════════════════════════════════
            // PASS 1: Fetch contacts with DedicatedCar=True and upsert guests
            // ══════════════════════════════════════════════════════════════════
            // Load field GUIDs from DB filtered by active event code
            const string defaultDedicatedCarGuid = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
            const string defaultRankGuid = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";
            var fieldMappings = await db.SyncFieldMappings
                .Where(f => f.EventCode == null || f.EventCode == eventCode)
                .ToListAsync(cancellationToken);
            var dedicatedCarGuid = (fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase) && f.EventCode == eventCode)
                ?? fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase)))
                ?.EventsAirFieldGuid ?? defaultDedicatedCarGuid;
            var rankGuid = (fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase) && f.EventCode == eventCode)
                ?? fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase)))
                ?.EventsAirFieldGuid ?? defaultRankGuid;
            _logger.LogInformation("EventsAir background sync using DedicatedCar GUID={DedicatedCarGuid}, Rank GUID={RankGuid} for event {EventCode}.",
                dedicatedCarGuid, rankGuid, eventCode);
            var contacts = await EventsAirSyncHelpers.FetchContactsWithDedicatedCarAsync(
                apiBaseUrl, eventCode, token, httpClientFactory, cancellationToken, dedicatedCarGuid, rankGuid);

            _logger.LogInformation("EventsAir background sync Pass 1: {Count} contacts with DedicatedCar=True.", contacts.Count);

            var syncedContactIds = new HashSet<string>(contacts.Select(c => c.ContactId), StringComparer.OrdinalIgnoreCase);

            // ── Bulk-load ALL guests keyed by EventsAirContactId (eliminates N per-contact SELECT) ──
            var existingGuestsByContactId = await db.Guests
                .ToDictionaryAsync(g => g.EventsAirContactId, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var contact in contacts)
            {
                if (string.IsNullOrEmpty(contact.ContactId)) continue;

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
                        IsActive = true,
                        Status = GuestStatus.Expected,
                        LastSyncedAt = DateTime.UtcNow
                    };
                    db.Guests.Add(newGuest);
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
                            db.SyncAlerts.Add(new SyncAlert
                            {
                                AlertType    = SyncAlertType.RegTypeChanged,
                                GuestId      = existing.Id,
                                GuestName    = $"{existing.FirstName} {existing.LastName}".Trim(),
                                EventsAirContactId = existing.EventsAirContactId,
                                OldValue     = existing.RegistrationTypeName,
                                NewValue     = contact.RegistrationTypeName,
                                SyncSource   = SyncAlertSource.AutoSync,
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
                    if (existing.DedicatedCar != "True") { existing.DedicatedCar = "True"; changed = true; }
                    if (!existing.IsActive) { existing.IsActive = true; changed = true; }
                    if (changed) { existing.LastSyncedAt = DateTime.UtcNow; updated++; }
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            // ══════════════════════════════════════════════════════════════════
            // PASS 2: Deactivate guests no longer in the DedicatedCar=True list
            //         Optimised: reuse existingGuestsByContactId from Pass 1 and
            //         load active-vehicle set in one query — no FindAsync per guest.
            // ══════════════════════════════════════════════════════════════════

            // Load active vehicle assignments keyed by GuestId for release on deactivation
            var activeAssignments = await db.VehicleAssignments
                .Where(va => va.IsActive)
                .Include(va => va.Vehicle)
                .ToListAsync(cancellationToken);
            var assignmentsByGuest = activeAssignments
                .GroupBy(va => va.GuestId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var kvp in existingGuestsByContactId)
            {
                var g = kvp.Value;
                if (!string.IsNullOrEmpty(g.EventsAirContactId) &&
                    !syncedContactIds.Contains(g.EventsAirContactId) &&
                    g.IsActive)
                {
                    // ── Release active vehicle assignment if present ──────────────────
                    string? vehiclePlate = null;
                    string? carClassName = null;
                    Guid? vehicleId = null;
                    if (assignmentsByGuest.TryGetValue(g.Id, out var assignment))
                    {
                        assignment.IsActive = false;
                        assignment.UnassignedAt = DateTime.UtcNow;
                        vehiclePlate = assignment.Vehicle?.LicensePlate;
                        vehicleId = assignment.VehicleId;
                        // Return vehicle to Available
                        if (assignment.Vehicle != null)
                        {
                            assignment.Vehicle.Status = VehicleStatus.Available;
                        }
                    }
                    // ── Create GuestRemoved SyncAlert ──────────────────────────────────
                    db.SyncAlerts.Add(new SyncAlert
                    {
                        AlertType    = SyncAlertType.GuestRemoved,
                        GuestId      = g.Id,
                        GuestName    = $"{g.FirstName} {g.LastName}".Trim(),
                        EventsAirContactId = g.EventsAirContactId,
                        VehicleId    = vehicleId,
                        VehiclePlate = vehiclePlate,
                        CarClassName = carClassName,
                        SyncSource   = SyncAlertSource.AutoSync,
                        DetectedAt   = DateTime.UtcNow
                    });
                    g.IsActive = false;
                    g.DedicatedCar = null;
                    g.LastSyncedAt = DateTime.UtcNow;
                    deactivated++;
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("EventsAir background sync Pass 2: {Deactivated} guests deactivated.", deactivated);

            // ══════════════════════════════════════════════════════════════════
            // PASS 3: Travel bookings — replace-on-rebooking with history
            //         Optimised: bulk-load all active guests (with bookings+flights)
            //         and all flights into dictionaries before the loop —
            //         eliminates 2N per-booking SELECT queries and N SaveChanges.
            // ══════════════════════════════════════════════════════════════════
            try
            {
                // Use per-contact batched query (avoids hanging global travelBookings query)
                var travelBookings = await EventsAirSyncHelpers.FetchTravelBookingsByContactsAsync(
                    apiBaseUrl, eventCode, token, httpClientFactory, syncedContactIds, cancellationToken);

                // ── Delegate all flight+booking processing to the shared helper ────────────
                // Uses the time-aware key (FlightNumber|Date|HHmm) so date AND time changes
                // in EventsAir are picked up correctly on the next sync.
                var syncResult = await EventsAirSyncHelpers.ProcessTravelBookingsAsync(db, travelBookings, cancellationToken);

                // Orphan cleanup is no longer needed because ProcessTravelBookingsAsync
                // now truncates and reloads all flights from scratch.

                await db.SaveChangesAsync(cancellationToken);
                travelSynced = syncResult.SavedNew + syncResult.UpdatedExisting + syncResult.Rebooked;
                _logger.LogInformation("EventsAir background sync Pass 3: {New} new, {Updated} updated, {Rebooked} rebooked, {Errors} errors.",
                    syncResult.SavedNew, syncResult.UpdatedExisting, syncResult.Rebooked, syncResult.ErrorCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EventsAir background sync Pass 3 (travel) failed — guest sync still succeeded.");
            }

            // ══════════════════════════════════════════════════════════════════
            // PASS 4: CarClassMismatch — detect active guests whose assigned vehicle
            //         car class no longer matches their DeservedCarClassId.
            //         Runs after every sync so it reflects the current state.
            //
            //         Uses Vehicle.CurrentGuestId + Status=Assigned as the
            //         authoritative source (denormalised field always kept in
            //         sync by all assignment paths). Falls back to the
            //         VehicleAssignments table for data-inconsistency cases.
            //         Skips guests who already have an open CarClassMismatch alert.
            // ══════════════════════════════════════════════════════════════════
            int carClassMismatches = 0;
            try
            {
                // Load all active guests that have a DeservedCarClassId set
                var guestsWithClass = await db.Guests
                    .Where(g => g.IsActive && g.DeservedCarClassId != null)
                    .Select(g => new { g.Id, g.FirstName, g.LastName, g.EventsAirContactId, g.DeservedCarClassId })
                    .ToListAsync(cancellationToken);

                // PRIMARY: vehicles currently assigned to a guest (Status=Assigned)
                // This is the authoritative source — always kept in sync by all
                // assignment code paths (FleetController, AssignVehicleHelper, etc.)
                var assignedVehiclesP4 = await db.Vehicles
                    .Where(v => v.Status == VehicleStatus.Assigned && v.CurrentGuestId != null)
                    .Include(v => v.CarClass)
                    .ToListAsync(cancellationToken);
                var vehicleByGuest = assignedVehiclesP4
                    .GroupBy(v => v.CurrentGuestId!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                // FALLBACK: if a vehicle's Status is not Assigned but a VehicleAssignment
                // record is still active (data inconsistency), include those too
                var activeVehicleAssignments = await db.VehicleAssignments
                    .Where(va => va.IsActive)
                    .Include(va => va.Vehicle).ThenInclude(v => v.CarClass)
                    .ToListAsync(cancellationToken);
                foreach (var va in activeVehicleAssignments)
                {
                    if (va.Vehicle == null) continue;
                    if (!vehicleByGuest.ContainsKey(va.GuestId))
                        vehicleByGuest[va.GuestId] = va.Vehicle;
                }

                // Load existing open CarClassMismatch alerts to avoid duplicates
                var existingOpenMismatchList = await db.SyncAlerts
                    .Where(a => a.AlertType == SyncAlertType.CarClassMismatch && !a.IsResolved && a.GuestId != null)
                    .Select(a => a.GuestId!.Value)
                    .ToListAsync(cancellationToken);
                var existingOpenMismatchGuestIds = new HashSet<Guid>(existingOpenMismatchList);

                foreach (var guest in guestsWithClass)
                {
                    // Skip if already has an open mismatch alert
                    if (existingOpenMismatchGuestIds.Contains(guest.Id)) continue;

                    // Skip if no assigned vehicle found via either source
                    if (!vehicleByGuest.TryGetValue(guest.Id, out var assignedVehicle)) continue;

                    var vehicleCarClassId = assignedVehicle.CarClassId;
                    if (vehicleCarClassId == null) continue;

                    // Mismatch: assigned vehicle's class ≠ guest's deserved class
                    if (vehicleCarClassId != guest.DeservedCarClassId)
                    {
                        var deservedClass = await db.CarClasses.FindAsync(new object[] { guest.DeservedCarClassId!.Value }, cancellationToken);
                        var assignedClass  = assignedVehicle.CarClass;
                        db.SyncAlerts.Add(new SyncAlert
                        {
                            AlertType          = SyncAlertType.CarClassMismatch,
                            GuestId            = guest.Id,
                            GuestName          = $"{guest.FirstName} {guest.LastName}".Trim(),
                            EventsAirContactId = guest.EventsAirContactId,
                            VehicleId          = assignedVehicle.Id,
                            VehiclePlate       = assignedVehicle.LicensePlate,
                            CarClassName       = assignedClass?.Name,
                            OldValue           = deservedClass?.Name ?? guest.DeservedCarClassId.ToString(),
                            NewValue           = assignedClass?.Name ?? vehicleCarClassId.ToString(),
                            SyncSource         = SyncAlertSource.AutoSync,
                            DetectedAt         = DateTime.UtcNow
                        });
                        carClassMismatches++;
                    }
                }
                if (carClassMismatches > 0)
                    await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("EventsAir background sync Pass 4: {Mismatches} car class mismatch alert(s) created.", carClassMismatches);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EventsAir background sync Pass 4 (car class mismatch) failed — sync still succeeded: {Msg}\n{Stack}", ex.Message, ex.StackTrace);
            }

            sw.Stop();

            // ── Update last sync status ───────────────────────────────────────
            var message = $"Added: {added}, Updated: {updated}, Deactivated: {deactivated}, Travel: {travelSynced}, CarClassMismatches: {carClassMismatches}";
            config.LastSyncAt = DateTime.UtcNow;
            config.LastSyncStatus = "Success";
            config.LastSyncMessage = message;
            config.LastSyncRecordsCount = added + updated;
            config.LastSyncDeactivatedCount = deactivated;
            await db.SaveChangesAsync(CancellationToken.None);

            // ── Write sync log entry ──────────────────────────────────────────
            await WriteSyncLogAsync(db, "Success", message, added + updated, deactivated, (int)sw.ElapsedMilliseconds,
                added: added, updated: updated, travelSynced: travelSynced,
                triggerSource: isStartupSync ? "Startup" : "System Auto-Sync", syncType: "Scheduled");
            await _systemLogService.LogAsync(LogSeverity.Information, "EventsAir Sync",
                $"Sync completed successfully in {sw.ElapsedMilliseconds}ms",
                $"Added: {added}, Updated: {updated}, Deactivated: {deactivated}, Travel bookings: {travelSynced}");
            _logger.LogInformation(
                "EventsAir background sync completed. Added={Added}, Updated={Updated}, Deactivated={Deactivated}, Travel={Travel}.",
                added, updated, deactivated, travelSynced);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "EventsAir background sync failed.");
            config.LastSyncAt = DateTime.UtcNow;
            config.LastSyncStatus = "Failed";
            config.LastSyncMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);
            await WriteSyncLogAsync(db, "Failed", ex.Message, 0, 0, (int)sw.ElapsedMilliseconds,
                triggerSource: isStartupSync ? "Startup" : "System Auto-Sync", syncType: "Scheduled");
            await _systemLogService.LogAsync(LogSeverity.Error, "EventsAir Sync",
                "Sync failed", ex.ToString());
        }
    }

    /// <summary>
    /// Writes a single row to EventsAirSyncLogs so the UI "Sync Logs" table shows background runs.
    /// Uses a fresh SaveChangesAsync with CancellationToken.None so it always persists.
    /// </summary>
    private static async Task WriteSyncLogAsync(
        AppDbContext db, string status, string message, int recordsSynced, int deactivated, int durationMs,
        int added = 0, int updated = 0, int travelSynced = 0,
        string triggerSource = "System Auto-Sync", string syncType = "Scheduled",
        Guid? initiatedByStaffId = null, string? initiatedByStaffName = null)
    {
        try
        {
            db.EventsAirSyncLogs.Add(new EventsAirSyncLog
            {
                SyncedAt = DateTime.UtcNow,
                Status = status,
                Message = message,
                RecordsSynced = recordsSynced,
                DurationMs = durationMs,
                SyncType = syncType,
                TriggerSource = triggerSource,
                InitiatedByStaffId = initiatedByStaffId,
                InitiatedByStaffName = initiatedByStaffName,
                RecordsAdded = added,
                RecordsUpdated = updated,
                RecordsDeactivated = deactivated,
                TravelBookingsSynced = travelSynced
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Log writing is best-effort; never let it crash the sync loop
        }
    }
}
