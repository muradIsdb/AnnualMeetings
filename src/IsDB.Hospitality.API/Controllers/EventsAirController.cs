using IsDB.Hospitality.Application.DTOs.EventsAir;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public EventsAirController(IAppDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
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
            IsActive = config.IsActive
        });
    }

    // PUT /api/eventsair/config
    [HttpPut("config")]
    public async Task<ActionResult<EventsAirConfigDto>> UpdateConfig([FromBody] UpdateEventsAirConfigRequest request)
    {
        var config = await _db.EventsAirConfigs.FirstOrDefaultAsync();

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

        return Ok(new EventsAirConfigDto
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
            IsActive = config.IsActive
        });
    }

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
        const string eventsAirScope = "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";

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
                config.ClientId, config.ClientSecret, _httpClientFactory);

            // ── Pass 1: Upsert guests with DedicatedCar=True ──────────────────
            var contacts = await Application.Common.Models.EventsAirSyncHelpers.FetchContactsWithDedicatedCarAsync(
                config.ApiBaseUrl, config.EventCode, token, _httpClientFactory, cancellationToken);

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

            // ── Pass 2: Deactivate guests no longer in the list ───────────────
            var activeGuests = await _db.Guests
                .Where(g => g.IsActive)
                .Include(g => g.VehicleAssignments)
                .Select(g => new
                {
                    g.Id,
                    g.EventsAirContactId,
                    HasActiveVehicle = g.VehicleAssignments.Any(va => va.IsActive)
                })
                .ToListAsync(cancellationToken);

            foreach (var dbGuest in activeGuests)
            {
                if (!string.IsNullOrEmpty(dbGuest.EventsAirContactId) &&
                    !syncedContactIds.Contains(dbGuest.EventsAirContactId) &&
                    !dbGuest.HasActiveVehicle)
                {
                    var g = await _db.Guests.FindAsync(new object[] { dbGuest.Id }, cancellationToken);
                    if (g != null)
                    {
                        g.IsActive = false;
                        g.DedicatedCar = null;
                        g.LastSyncedAt = DateTime.UtcNow;
                        deactivated++;
                    }
                }
            }
            await _db.SaveChangesAsync(cancellationToken);

            // ── Pass 3: Travel bookings ───────────────────────────────────────
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

        _db.EventsAirSyncLogs.Add(new EventsAirSyncLog
        {
            SyncedAt = DateTime.UtcNow,
            Status = status,
            Message = message,
            RecordsSynced = added + updated,
            DurationMs = (int)sw.ElapsedMilliseconds,
            SyncType = "Manual"
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
    public async Task<ActionResult<List<EventsAirSyncLogDto>>> GetSyncLogs([FromQuery] int limit = 20)
    {
        var logs = await _db.EventsAirSyncLogs
            .OrderByDescending(l => l.SyncedAt)
            .Take(limit)
            .Select(l => new EventsAirSyncLogDto
            {
                Id = l.Id,
                SyncedAt = l.SyncedAt,
                Status = l.Status,
                Message = l.Message,
                RecordsSynced = l.RecordsSynced,
                DurationMs = l.DurationMs,
                SyncType = l.SyncType
            })
            .ToListAsync();

        return Ok(logs);
    }
}
