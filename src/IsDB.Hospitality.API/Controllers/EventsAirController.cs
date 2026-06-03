using IsDB.Hospitality.Application.DTOs.EventsAir;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Linq;

namespace IsDB.Hospitality.API.Controllers;

[Authorize(Roles = "Admin")]
public class EventsAirController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly AppDbContext _appDb;
    private readonly IHttpClientFactory _httpClientFactory;

    public EventsAirController(IAppDbContext db, AppDbContext appDb, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _appDb = appDb;
        _httpClientFactory = httpClientFactory;
    }

    // Helper: read OAuthScope from DB via raw SQL (column is NotMapped on entity to avoid breaking existing deployments)
    private async Task<string> GetOAuthScopeAsync()
    {
        const string defaultScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        try
        {
            var conn = _appDb.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"OAuthScope\" FROM \"EventsAirConfigs\" LIMIT 1";
            var result = await cmd.ExecuteScalarAsync();
            return result is string s && !string.IsNullOrWhiteSpace(s) ? s : defaultScope;
        }
        catch
        {
            return defaultScope;
        }
    }

    // Helper: save OAuthScope to DB via raw SQL
    private async Task SaveOAuthScopeAsync(Guid configId, string scope)
    {
        try
        {
            await _appDb.Database.ExecuteSqlRawAsync(
                "UPDATE \"EventsAirConfigs\" SET \"OAuthScope\" = {0} WHERE \"Id\" = {1}",
                scope, configId);
        }
        catch { /* column may not exist yet on very first deployment */ }
    }

    // GET /api/eventsair/config
    [HttpGet("config")]
    public async Task<ActionResult<EventsAirConfigDto>> GetConfig()
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        if (config == null)
        {
            // Return default empty config if none exists
            return Ok(new EventsAirConfigDto
            {
                Id = Guid.Empty,
                ClientId = "",
                ClientSecret = "",
                ApiBaseUrl = "https://api.eventsair.com",
                TokenEndpoint = "https://auth.eventsair.com/connect/token",
                EventCode = "",
                TenantCode = "",
                SyncIntervalMinutes = 15,
                AutoSyncEnabled = false,
                SyncOnStartup = false,
                LastSyncStatus = "Never",
                IsActive = false
            });
        }

        return Ok(new EventsAirConfigDto
        {
            Id = config.Id,
            ClientId = config.ClientId,
            ClientSecret = string.IsNullOrEmpty(config.ClientSecret) ? "" : "••••••••••••",
            ApiBaseUrl = config.ApiBaseUrl,
            TokenEndpoint = config.TokenEndpoint,
            EventCode = config.EventCode,
            TenantCode = config.TenantCode,
            SyncIntervalMinutes = config.SyncIntervalMinutes,
            AutoSyncEnabled = config.AutoSyncEnabled,
            SyncOnStartup = config.SyncOnStartup,
            LastSyncAt = config.LastSyncAt,
            LastSyncStatus = config.LastSyncStatus,
            LastSyncMessage = config.LastSyncMessage,
            LastSyncRecordsCount = config.LastSyncRecordsCount,
            IsActive = config.IsActive,
            OAuthScope = await GetOAuthScopeAsync()
        });
    }

    // PUT /api/eventsair/config
    [HttpPut("config")]
    public async Task<ActionResult<object>> UpdateConfig([FromBody] UpdateEventsAirConfigRequest request)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        var previousEventCode = config?.EventCode;

        if (config == null)
        {
            config = new EventsAirConfig
            {
                CreatedAt = DateTime.UtcNow
            };
            _db.EventsAirConfigs.Add(config);
        }

        config.ClientId = request.ClientId;
        if (!string.IsNullOrEmpty(request.ClientSecret))
            config.ClientSecret = request.ClientSecret;
        config.ApiBaseUrl = request.ApiBaseUrl;
        config.TokenEndpoint = request.TokenEndpoint;
        config.EventCode = request.EventCode;
        config.TenantCode = request.TenantCode;
        config.SyncIntervalMinutes = request.SyncIntervalMinutes;
        config.AutoSyncEnabled = request.AutoSyncEnabled;
        config.SyncOnStartup = request.SyncOnStartup;
        config.IsActive = request.IsActive;
        config.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);

        // Save OAuthScope via raw SQL since the property is NotMapped
        if (!string.IsNullOrWhiteSpace(request.OAuthScope))
            await SaveOAuthScopeAsync(config.Id, request.OAuthScope.Trim());

        // Detect event switch
        bool eventSwitched = !string.IsNullOrEmpty(request.EventCode) &&
                             !string.IsNullOrEmpty(previousEventCode) &&
                             request.EventCode != previousEventCode;
        bool newEventHasCarClasses = false;
        bool newEventHasVehicles = false;
        if (eventSwitched)
        {
            newEventHasCarClasses = await _db.CarClasses
                .AnyAsync(c => c.EventCode == request.EventCode, CancellationToken.None);
            newEventHasVehicles = await _db.Vehicles
                .AnyAsync(v => v.IsActive && v.EventCode == request.EventCode, CancellationToken.None);
        }

        var dto = new EventsAirConfigDto
        {
            Id = config.Id,
            ClientId = config.ClientId,
            ClientSecret = "••••••••••••",
            ApiBaseUrl = config.ApiBaseUrl,
            TokenEndpoint = config.TokenEndpoint,
            EventCode = config.EventCode,
            TenantCode = config.TenantCode,
            SyncIntervalMinutes = config.SyncIntervalMinutes,
            AutoSyncEnabled = config.AutoSyncEnabled,
            SyncOnStartup = config.SyncOnStartup,
            LastSyncAt = config.LastSyncAt,
            LastSyncStatus = config.LastSyncStatus,
            LastSyncMessage = config.LastSyncMessage,
            LastSyncRecordsCount = config.LastSyncRecordsCount,
            IsActive = config.IsActive,
            OAuthScope = await GetOAuthScopeAsync()
        };

        return Ok(new
        {
            config = dto,
            eventSwitched,
            newEventHasCarClasses,
            newEventHasVehicles,
            previousEventCode
        });
    }

    // POST /api/eventsair/config/apply-event-switch
    [HttpPost("config/apply-event-switch")]
    public async Task<IActionResult> ApplyEventSwitch(
        [FromBody] ApplyEventSwitchRequest request,
        CancellationToken ct)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(ct);
        if (config == null) return BadRequest("No EventsAir configuration found.");

        var newEventCode = config.EventCode;
        var previousEventCode = request.PreviousEventCode;

        // 1. Reset all guest DeservedCarClassId
        var allGuests = await _db.Guests.Where(g => g.IsActive).ToListAsync(ct);
        foreach (var g in allGuests)
            g.DeservedCarClassId = null;

        // 2. Reset hotel room counts
        var hotels = await _db.HotelOptions.ToListAsync(ct);
        foreach (var h in hotels)
        {
            h.ContractedRoomsIsDB = 0;
            h.ContractedRoomsGuest = 0;
            h.ActualOccupiedIsDB = 0;
            h.ActualOccupiedGuest = 0;
        }

        // 3. Stamp all NULL-EventCode records (guests + fleet) with previousEventCode so they are scoped to the old event
        if (!string.IsNullOrEmpty(previousEventCode))
        {
            var nullEventGuests = await _db.Guests.Where(g => g.EventCode == null).ToListAsync(ct);
            foreach (var g in nullEventGuests)
                g.EventCode = previousEventCode;

            var nullEventVehicles = await _db.Vehicles.Where(v => v.EventCode == null).ToListAsync(ct);
            foreach (var v in nullEventVehicles)
                v.EventCode = previousEventCode;

            var nullEventDrivers = await _db.Drivers.Where(d => d.EventCode == null).ToListAsync(ct);
            foreach (var d in nullEventDrivers)
                d.EventCode = previousEventCode;

            var nullEventCarClasses = await _db.CarClasses.Where(c => c.EventCode == null).ToListAsync(ct);
            foreach (var c in nullEventCarClasses)
                c.EventCode = previousEventCode;

            var nullEventCarClassRules = await _db.CarClassRules.Where(r => r.EventCode == null).ToListAsync(ct);
            foreach (var r in nullEventCarClassRules)
                r.EventCode = previousEventCode;

            var nullEventFieldMappings = await _db.SyncFieldMappings.Where(m => m.EventCode == null).ToListAsync(ct);
            foreach (var m in nullEventFieldMappings)
                m.EventCode = previousEventCode;
        }

        // 4. Optionally copy Car Classes from previous event
        if (request.CopyCarClasses && !string.IsNullOrEmpty(previousEventCode))
        {
            var previousCarClasses = await _db.CarClasses
                .Where(c => c.EventCode == previousEventCode)
                .ToListAsync(ct);

            foreach (var cc in previousCarClasses)
            {
                // Only copy if the new event doesn't already have a class with the same name
                bool alreadyExists = await _db.CarClasses
                    .AnyAsync(c => c.EventCode == newEventCode && c.Name == cc.Name, ct);
                if (!alreadyExists)
                {
                    _db.CarClasses.Add(new CarClass
                    {
                        Id = Guid.NewGuid(),
                        Name = cc.Name,
                        Description = cc.Description,
                        Color = cc.Color,
                        SortOrder = cc.SortOrder,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 5. Optionally copy Fleet (vehicles + drivers) from previous event
        if (request.CopyFleet && !string.IsNullOrEmpty(previousEventCode))
        {
            var previousVehicles = await _db.Vehicles
                .Where(v => v.IsActive && v.EventCode == previousEventCode)
                .ToListAsync(ct);

            // Load existing 2026 vehicle identifiers to avoid duplicates
            var existingNewEventVehicleKeys = await _db.Vehicles
                .Where(x => x.EventCode == newEventCode)
                .Select(x => new { x.LicensePlate, x.CarNumber })
                .ToListAsync(ct);
            var existingVehicleSet = new HashSet<string>(
                existingNewEventVehicleKeys.Select(x => $"{x.LicensePlate}|{x.CarNumber}"));

            foreach (var v in previousVehicles)
            {
                var key = $"{v.LicensePlate}|{v.CarNumber}";
                if (!existingVehicleSet.Contains(key))
                {
                    existingVehicleSet.Add(key); // prevent duplicates within the batch
                    _db.Vehicles.Add(new Vehicle
                    {
                        Id = Guid.NewGuid(),
                        LicensePlate = v.LicensePlate,
                        Make = v.Make,
                        Model = v.Model,
                        Color = v.Color,
                        CarNumber = v.CarNumber,
                        BarcodeValue = v.BarcodeValue,
                        Status = VehicleStatus.Available,
                        IsActive = true,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            var previousDrivers = await _db.Drivers
                .Where(d => d.IsActive && d.EventCode == previousEventCode)
                .ToListAsync(ct);

            var existingNewEventDriverNames = await _db.Drivers
                .Where(x => x.EventCode == newEventCode)
                .Select(x => x.FullName)
                .ToListAsync(ct);
            var existingDriverSet = new HashSet<string>(existingNewEventDriverNames, StringComparer.OrdinalIgnoreCase);

            foreach (var d in previousDrivers)
            {
                if (!existingDriverSet.Contains(d.FullName))
                {
                    existingDriverSet.Add(d.FullName);
                    _db.Drivers.Add(new Driver
                    {
                        Id = Guid.NewGuid(),
                        FullName = d.FullName,
                        Phone = d.Phone,
                        Status = DriverStatus.Available,
                        IsActive = true,
                        EventCode = newEventCode,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Event switch applied successfully." });
    }

    public record ApplyEventSwitchRequest(
        string PreviousEventCode,
        bool CopyCarClasses,
        bool CopyFleet = false
    );

    // POST /api/eventsair/test-connection
    [HttpPost("test-connection")]
    public async Task<ActionResult<TestConnectionResult>> TestConnection([FromBody] TestConnectionRequest? request)
    {
        if (request == null)
        {
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Invalid request. Please provide connection details.",
                ResponseTimeMs = 0
            });
        }

        // If ClientSecret is blank, fall back to the saved secret in the database
        var effectiveSecret = request.ClientSecret;
        if (string.IsNullOrWhiteSpace(effectiveSecret))
        {
            var savedConfig = await _db.EventsAirConfigs.FirstOrDefaultAsync();
            effectiveSecret = savedConfig?.ClientSecret ?? string.Empty;
        }

        // If ClientId is also blank, try to use saved values
        var effectiveClientId = string.IsNullOrWhiteSpace(request.ClientId)
            ? (await _db.EventsAirConfigs.FirstOrDefaultAsync())?.ClientId ?? string.Empty
            : request.ClientId;

        if (string.IsNullOrWhiteSpace(effectiveClientId) || string.IsNullOrWhiteSpace(effectiveSecret))
        {
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Client ID and Client Secret are required. Please save your credentials first, then test the connection.",
                ResponseTimeMs = 0
            });
        }

        // EventsAir uses Microsoft Azure AD for OAuth2 — the stored TokenEndpoint (auth.eventsair.com)
        // does not resolve; always use the correct Azure AD endpoint and scope.
        const string azureAdTokenEndpoint = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
        var eventsAirScope = await GetOAuthScopeAsync();

        var sw = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", effectiveClientId),
                new KeyValuePair<string, string>("client_secret", effectiveSecret),
                new KeyValuePair<string, string>("scope", eventsAirScope)
            });

            var response = await client.PostAsync(azureAdTokenEndpoint, tokenRequest);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Extract a token preview (first 20 chars)
                var tokenPreview = content.Length > 50
                    ? content.Substring(0, 50) + "..."
                    : content;

                return Ok(new TestConnectionResult
                {
                    Success = true,
                    Message = "Connection successful. OAuth2 token retrieved via Microsoft Azure AD for EventsAir.",
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds,
                    TokenPreview = tokenPreview
                });
            }
            else
            {
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = $"Authentication failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Please verify your Client ID and Client Secret.",
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds
                });
            }
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = "Connection timed out after 10 seconds. Please verify the Token Endpoint URL is reachable.",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new TestConnectionResult
            {
                Success = false,
                Message = $"Connection error: {ex.Message}",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            });
        }
    }

    // POST /api/eventsair/sync
    // Runs the full 3-pass sync (contacts → deactivate → travel bookings) synchronously.
    // The UI "Sync Now" button calls /guests/sync-from-eventsair (async job) instead,
    // but this endpoint is kept for direct API / admin use and must not be a stub.
    [HttpPost("sync")]
    public async Task<ActionResult<TriggerSyncResult>> TriggerSync(CancellationToken cancellationToken)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null || !config.IsActive)
        {
            return BadRequest(new TriggerSyncResult
            {
                Success = false,
                Message = "EventsAir integration is not configured or is inactive. Please save a valid configuration first."
            });
        }

        if (string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret) ||
            string.IsNullOrWhiteSpace(config.EventCode) || string.IsNullOrWhiteSpace(config.ApiBaseUrl))
        {
            return BadRequest(new TriggerSyncResult
            {
                Success = false,
                Message = "EventsAir credentials or EventCode are not configured."
            });
        }

        var sw = Stopwatch.StartNew();
        int added = 0, updated = 0, deactivated = 0, travelSynced = 0;
        string status;
        string message;

        try
        {
            // ── Acquire token ─────────────────────────────────────────────────
            var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
                config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());

            // ── Load event-scoped field GUIDs from DB ─────────────────────────
            const string defaultDedicatedCarGuid = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
            const string defaultRankGuid = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";
            var fieldMappings = await _db.SyncFieldMappings
                .Where(f => f.EventCode == null || f.EventCode == config.EventCode)
                .ToListAsync(cancellationToken);
            var dedicatedCarGuid = (fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
                ?? fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Dedicated Car", StringComparison.OrdinalIgnoreCase)))
                ?.EventsAirFieldGuid ?? defaultDedicatedCarGuid;
            var rankGuid = (fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase) && f.EventCode == config.EventCode)
                ?? fieldMappings.FirstOrDefault(f =>
                    f.DisplayName.Equals("Rank", StringComparison.OrdinalIgnoreCase)))
                ?.EventsAirFieldGuid ?? defaultRankGuid;

            // ── Pass 1: Upsert guests with DedicatedCar=True ──────────────────
            var contacts = await Application.Common.Models.EventsAirSyncHelpers.FetchContactsWithDedicatedCarAsync(
                config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, cancellationToken, dedicatedCarGuid, rankGuid);

            var syncedContactIds = new HashSet<string>(contacts.Select(c => c.ContactId), StringComparer.OrdinalIgnoreCase);

            foreach (var contact in contacts)
            {
                if (string.IsNullOrEmpty(contact.ContactId)) continue;

                var existing = await _db.Guests.FirstOrDefaultAsync(
                    g => g.EventsAirContactId == contact.ContactId, cancellationToken);

                if (existing == null)
                {
                    _db.Guests.Add(new Domain.Entities.Guest
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
                        Status = Domain.Enums.GuestStatus.Expected,
                        LastSyncedAt = DateTime.UtcNow
                    });
                    added++;
                }
                else
                {
                    bool changed = false;
                    if (existing.FirstName != contact.FirstName) { existing.FirstName = contact.FirstName; changed = true; }
                    if (existing.LastName != contact.LastName) { existing.LastName = contact.LastName; changed = true; }
                    if (existing.Designation != contact.JobTitle) { existing.Designation = contact.JobTitle; changed = true; }
                    if (existing.Organization != contact.OrganizationName) { existing.Organization = contact.OrganizationName; changed = true; }
                    if (existing.RegistrationTypeName != contact.RegistrationTypeName) { existing.RegistrationTypeName = contact.RegistrationTypeName; changed = true; }
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
            await _db.SaveChangesAsync(cancellationToken);

            // ── Pass 2: Deactivate guests no longer in the list ────────────────────
            var activeGuests = await _db.Guests
                .Where(g => g.IsActive)
                .Select(g => new { g.Id, g.EventsAirContactId })
                .ToListAsync(cancellationToken);

            foreach (var dbGuest in activeGuests)
            {
                if (!string.IsNullOrEmpty(dbGuest.EventsAirContactId) &&
                    !syncedContactIds.Contains(dbGuest.EventsAirContactId))
                {
                    var g = await _db.Guests
                        .Include(x => x.VehicleAssignments)
                        .FirstOrDefaultAsync(x => x.Id == dbGuest.Id, cancellationToken);
                    if (g != null)
                    {
                        // Deactivate any active vehicle assignments to release vehicles back to pool
                        foreach (var va in g.VehicleAssignments.Where(va => va.IsActive))
                        {
                            va.IsActive = false;
                            va.UnassignedAt = DateTime.UtcNow;
                            // Release the vehicle
                            var vehicle = await _db.Vehicles.FindAsync(new object[] { va.VehicleId }, cancellationToken);
                            if (vehicle != null)
                            {
                                vehicle.Status = Domain.Enums.VehicleStatus.Available;
                                vehicle.CurrentGuestId = null;
                                vehicle.CurrentAssignmentType = null;
                            }
                        }
                        g.IsActive = false;
                        g.DedicatedCar = null;
                        g.LastSyncedAt = DateTime.UtcNow;
                        deactivated++;
                    }
                }
            }
            await _db.SaveChangesAsync(cancellationToken);

            // ── Pass 3: Travel bookings ───────────────────────────────────────────────
            try
            {
                var travelBookings = await Application.Common.Models.EventsAirSyncHelpers.FetchTravelBookingsAsync(
                    config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, cancellationToken);

                foreach (var tbDto in travelBookings)
                {
                    if (string.IsNullOrEmpty(tbDto.FlightNumber) || string.IsNullOrEmpty(tbDto.ContactId)) continue;

                    var guest = await _db.Guests
                        .Include(g => g.TravelBookings)
                        .ThenInclude(tb => tb.Flight)
                        .FirstOrDefaultAsync(g => g.EventsAirContactId == tbDto.ContactId, cancellationToken);

                    if (guest == null || !guest.IsActive) continue;

                    bool isArrival = tbDto.TravelTypeName?.Contains("Arrival", StringComparison.OrdinalIgnoreCase) ?? true;

                    DateTime? scheduledArrival = null;
                    DateTime? scheduledDeparture = null;

                    if (isArrival && !string.IsNullOrEmpty(tbDto.ArrivalDate) &&
                        DateTime.TryParse(tbDto.ArrivalDate, out var arrDate))
                        scheduledArrival = !string.IsNullOrEmpty(tbDto.Eta) && TimeSpan.TryParse(tbDto.Eta, out var etaTime)
                            ? arrDate.Add(etaTime) : arrDate;

                    if (!isArrival && !string.IsNullOrEmpty(tbDto.DepartureDate) &&
                        DateTime.TryParse(tbDto.DepartureDate, out var depDate))
                        scheduledDeparture = !string.IsNullOrEmpty(tbDto.Etd) && TimeSpan.TryParse(tbDto.Etd, out var etdTime)
                            ? depDate.Add(etdTime) : depDate;

                    var flight = await _db.Flights.FirstOrDefaultAsync(f => f.FlightNumber == tbDto.FlightNumber, cancellationToken);
                    if (flight == null)
                    {
                        flight = new Domain.Entities.Flight
                        {
                            FlightNumber = tbDto.FlightNumber,
                            AirlineName = tbDto.CarrierName ?? "Unknown",
                            ScheduledArrival = scheduledArrival ?? DateTime.MinValue,
                            ScheduledDeparture = scheduledDeparture ?? DateTime.MinValue,
                            ArrivalPortName = tbDto.ArrivalPortName,
                            DeparturePortName = tbDto.DeparturePortName,
                            Status = Domain.Enums.FlightStatus.Scheduled
                        };
                        _db.Flights.Add(flight);
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    else
                    {
                        if (scheduledArrival.HasValue) flight.ScheduledArrival = scheduledArrival.Value;
                        if (scheduledDeparture.HasValue) flight.ScheduledDeparture = scheduledDeparture.Value;
                        if (!string.IsNullOrEmpty(tbDto.ArrivalPortName)) flight.ArrivalPortName = tbDto.ArrivalPortName;
                        if (!string.IsNullOrEmpty(tbDto.DeparturePortName)) flight.DeparturePortName = tbDto.DeparturePortName;
                        if (!string.IsNullOrEmpty(tbDto.CarrierName)) flight.AirlineName = tbDto.CarrierName;
                    }

                    var existingBooking = guest.TravelBookings.FirstOrDefault(b => b.IsArrival == isArrival);
                    var notes = tbDto.BookingNotes ?? tbDto.Comment;

                    if (existingBooking == null)
                    {
                        _db.TravelBookings.Add(new Domain.Entities.TravelBooking
                        {
                            GuestId = guest.Id,
                            FlightId = flight.Id,
                            IsArrival = isArrival,
                            SeatClass = tbDto.SeatClass,
                            BookingNotes = notes,
                            LastSyncedAt = DateTime.UtcNow
                        });
                    }
                    else if (existingBooking.FlightId != flight.Id)
                    {
                        _db.TravelBookingHistories.Add(new Domain.Entities.TravelBookingHistory
                        {
                            Id = Guid.NewGuid(),
                            TravelBookingId = existingBooking.Id,
                            GuestId = guest.Id,
                            PreviousFlightNumber = existingBooking.Flight?.FlightNumber ?? "",
                            PreviousAirlineName = existingBooking.Flight?.AirlineName,
                            PreviousScheduledArrival = existingBooking.Flight?.ScheduledArrival,
                            PreviousScheduledDeparture = existingBooking.Flight?.ScheduledDeparture,
                            PreviousDeparturePort = existingBooking.Flight?.DeparturePortName,
                            PreviousArrivalPort = existingBooking.Flight?.ArrivalPortName,
                            PreviousSeatClass = existingBooking.SeatClass,
                            ChangedAt = DateTime.UtcNow
                        });
                        existingBooking.FlightId = flight.Id;
                        existingBooking.SeatClass = tbDto.SeatClass;
                        existingBooking.BookingNotes = notes;
                        existingBooking.ChangedSinceLastView = true;
                        existingBooking.PreviousFlightNumber = existingBooking.Flight?.FlightNumber;
                        existingBooking.ChangedAt = DateTime.UtcNow;
                        existingBooking.LastSyncedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        existingBooking.SeatClass = tbDto.SeatClass;
                        existingBooking.BookingNotes = notes;
                        existingBooking.LastSyncedAt = DateTime.UtcNow;
                    }

                    travelSynced++;
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Pass 3 failure is non-fatal — guest sync already succeeded
                _ = ex;
            }

            sw.Stop();
            status = "Success";
            message = $"Added: {added}, Updated: {updated}, Deactivated: {deactivated}, Travel: {travelSynced}";
        }
        catch (Exception ex)
        {
            sw.Stop();
            status = "Failed";
            message = ex.Message;
        }

        // ── Persist result ────────────────────────────────────────────────────
        config.LastSyncAt = DateTime.UtcNow;
        config.LastSyncStatus = status;
        config.LastSyncMessage = message;
        config.LastSyncRecordsCount = added + updated;
        config.LastSyncDeactivatedCount = deactivated;

        // Capture caller identity for the log
        var staffIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var staffId = staffIdClaim != null && Guid.TryParse(staffIdClaim, out var sid) ? sid : (Guid?)null;
        var staffName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                     ?? User.FindFirst("name")?.Value
                     ?? User.Identity?.Name;

        _db.EventsAirSyncLogs.Add(new EventsAirSyncLog
        {
            SyncedAt = DateTime.UtcNow,
            Status = status,
            Message = message,
            RecordsSynced = added + updated,
            DurationMs = (int)sw.ElapsedMilliseconds,
            SyncType = "Manual",
            TriggerSource = "API Trigger",
            InitiatedByStaffId = staffId,
            InitiatedByStaffName = staffName,
            RecordsAdded = added,
            RecordsUpdated = updated,
            RecordsDeactivated = deactivated,
            TravelBookingsSynced = travelSynced
        });

        await _db.SaveChangesAsync(CancellationToken.None);

        if (status == "Failed")
            return StatusCode(500, new TriggerSyncResult
            {
                Success = false,
                Message = message,
                RecordsSynced = 0,
                DurationMs = (int)sw.ElapsedMilliseconds
            });

        return Ok(new TriggerSyncResult
        {
            Success = true,
            Message = message,
            RecordsSynced = added + updated,
            DurationMs = (int)sw.ElapsedMilliseconds
        });
    }

    // GET /api/eventsair/sync-logs
    [HttpGet("sync-logs")]
    public async Task<IActionResult> GetSyncLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? syncType = null,
        [FromQuery] string? triggerSource = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var query = _db.EventsAirSyncLogs.AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(syncType))
            query = query.Where(l => l.SyncType == syncType);
        if (!string.IsNullOrWhiteSpace(triggerSource))
            query = query.Where(l => l.TriggerSource == triggerSource);
        if (startDate.HasValue)
            query = query.Where(l => l.SyncedAt >= startDate.Value.ToUniversalTime());
        if (endDate.HasValue)
            query = query.Where(l => l.SyncedAt <= endDate.Value.ToUniversalTime().AddDays(1));

        // Search (message or staff name)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(l =>
                (l.Message != null && l.Message.ToLower().Contains(s)) ||
                (l.InitiatedByStaffName != null && l.InitiatedByStaffName.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.SyncedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new EventsAirSyncLogDto
            {
                Id = l.Id,
                SyncedAt = l.SyncedAt,
                Status = l.Status,
                Message = l.Message,
                RecordsSynced = l.RecordsSynced,
                DurationMs = l.DurationMs,
                SyncType = l.SyncType,
                TriggerSource = l.TriggerSource,
                InitiatedByStaffName = l.InitiatedByStaffName,
                RecordsAdded = l.RecordsAdded,
                RecordsUpdated = l.RecordsUpdated,
                RecordsDeactivated = l.RecordsDeactivated,
                TravelBookingsSynced = l.TravelBookingsSynced
            })
            .ToListAsync();

        return Ok(new
        {
            items = logs,
            totalCount,
            totalPages,
            page,
            pageSize
        });
    }

    // GET /api/eventsair/events
    // Returns all EventsAir events whose name contains "annual" (case-insensitive),
    // sorted by startDate descending. Used to populate the Event Code dropdown in the config UI.
    [HttpGet("events")]
    public async Task<ActionResult<List<Application.DTOs.EventsAir.EventsAirEventDto>>> GetEvents()
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();
        if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            return Ok(new List<Application.DTOs.EventsAir.EventsAirEventDto>());
        }

        try
        {
            var token = await Application.Common.Models.EventsAirSyncHelpers.GetEventsAirTokenAsync(
                config.ClientId, config.ClientSecret, _httpClientFactory, await GetOAuthScopeAsync());

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Query EventsAir for events whose name contains "Annual" (case-sensitive CONTAINS)
            // We build the JSON body manually to avoid escaping issues with verbatim strings
            var queryBody = "{\"query\":\"{events(input:{where:{name:{comparisonType:CONTAINS,value:\\\"Annual\\\"}}}limit:200 offset:0){uniqueCode name startDate endDate}}\"}";
            // Use JsonSerializer to build a clean query body
            var gqlQuery = "{ events(input: { where: { name: { comparisonType: CONTAINS, value: \"Annual\" } } } limit: 200 offset: 0) { id name startDate endDate } }";
            queryBody = System.Text.Json.JsonSerializer.Serialize(new { query = gqlQuery });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { { "Authorization", $"Bearer {token}" } },
                Content = new StringContent(queryBody, System.Text.Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);

            var eventsArray = doc.GetProperty("data").GetProperty("events");
            var result = new List<Application.DTOs.EventsAir.EventsAirEventDto>();

            foreach (var ev in eventsArray.EnumerateArray())
            {
                var uniqueCode = ev.TryGetProperty("id", out var uc) ? uc.GetString() ?? "" : "";
                var name = ev.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var startDate = ev.TryGetProperty("startDate", out var sd) ? sd.GetString() : null;
                var endDate = ev.TryGetProperty("endDate", out var ed) ? ed.GetString() : null;

                result.Add(new Application.DTOs.EventsAir.EventsAirEventDto
                {
                    UniqueCode = uniqueCode,
                    Name = name,
                    StartDate = startDate,
                    EndDate = endDate
                });
            }

            // If no results with "Annual" filter, try without filter as fallback
            if (result.Count == 0)
            {
                var gqlQueryAll = "{ events(limit: 200 offset: 0) { id name startDate endDate } }";
                var queryBodyAll = System.Text.Json.JsonSerializer.Serialize(new { query = gqlQueryAll });
                var req2 = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/graphql")
                {
                    Headers = { { "Authorization", $"Bearer {token}" } },
                    Content = new StringContent(queryBodyAll, System.Text.Encoding.UTF8, "application/json")
                };
                var response2 = await client.SendAsync(req2);
                if (response2.IsSuccessStatusCode)
                {
                    var json2 = await response2.Content.ReadAsStringAsync();
                    var doc2 = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json2);
                    if (doc2.TryGetProperty("data", out var data2) && data2.TryGetProperty("events", out var eventsArray2))
                    {
                        foreach (var ev in eventsArray2.EnumerateArray())
                        {
                            var uniqueCode = ev.TryGetProperty("id", out var uc) ? uc.GetString() ?? "" : "";
                            var name = ev.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                            var startDate = ev.TryGetProperty("startDate", out var sd) ? sd.GetString() : null;
                            var endDate = ev.TryGetProperty("endDate", out var ed) ? ed.GetString() : null;
                            result.Add(new Application.DTOs.EventsAir.EventsAirEventDto
                            {
                                UniqueCode = uniqueCode,
                                Name = name,
                                StartDate = startDate,
                                EndDate = endDate
                            });
                        }
                    }
                }
            }

            // Sort by startDate descending (most recent first)
            result = result
                .OrderByDescending(e => e.StartDate ?? "")
                .ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            // Return 502 so the UI can distinguish "failed to load" from "genuinely no events"
            return StatusCode(502, new { error = "Failed to load events from EventsAir", detail = ex.Message });
        }
    }
}
