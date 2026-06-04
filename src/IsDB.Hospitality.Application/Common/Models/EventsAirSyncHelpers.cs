using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Common.Models;

/// <summary>
/// Shared static helpers for EventsAir sync operations.
/// Used by both the manual sync endpoint (GuestsController) and the background EventsAirSyncService.
/// </summary>
public static class EventsAirSyncHelpers
{
    public const string DedicatedCarFieldGuid = "d6b74b23-c8b6-d044-5d86-3a17bafe27de";
    public const string RankFieldGuid = "3d96b87e-87b0-145e-5f45-3a17bafe26d4";
    public const string VehicleTypeFieldGuid = "5f6b0e9e-7d1c-4f91-affc-ecbe95cef678";

    // ─── OAuth2 Token ─────────────────────────────────────────────────────────

    public static async Task<string> GetEventsAirTokenAsync(
        string clientId, string clientSecret, IHttpClientFactory httpClientFactory,
        string? oAuthScope = null)
    {
        var client = httpClientFactory.CreateClient();
        const string tokenUrl = "https://login.microsoftonline.com/dff76352-1ded-46e8-96a4-1a83718b2d3a/oauth2/v2.0/token";
        var scope = !string.IsNullOrWhiteSpace(oAuthScope)
            ? oAuthScope
            : "https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default";
        var tokenRequest = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", scope)
        });
        var response = await client.PostAsync(tokenUrl, tokenRequest);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        return doc.GetProperty("access_token").GetString()!;
    }

    // ─── Fetch contacts with DedicatedCar=True ────────────────────────────────

    public static async Task<List<EventsAirSyncContactDto>> FetchContactsWithDedicatedCarAsync(
        string baseUrl, string eventCode, string accessToken,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken,
        string? dedicatedCarGuid = null, string? rankGuid = null, string? vehicleTypeGuid = null)
    {
        dedicatedCarGuid ??= DedicatedCarFieldGuid;
        rankGuid ??= RankFieldGuid;
        vehicleTypeGuid ??= VehicleTypeFieldGuid;
        var fetched = new List<EventsAirSyncContactDto>();
        var seenContactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        int offset = 0;
        const int pageSize = 25;

        while (true)
        {
            var graphqlQuery = $@"{{
              event(id: ""{eventCode}"") {{
                contacts(input: {{ contactFilter: {{ customFields: {{ checkboxCustomFieldFilters: [{{ definitionId: ""{dedicatedCarGuid}"", isChecked: true }}] }} }} }}, offset: {offset}, limit: {pageSize}) {{
                  id firstName lastName title jobTitle organizationName primaryEmail
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
                throw new InvalidOperationException($"EventsAir API returned HTTP {(int)response.StatusCode}");

            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var errorMsg = errors[0].GetProperty("message").GetString() ?? "Unknown GraphQL error";
                if (errorMsg.Contains("cost", StringComparison.OrdinalIgnoreCase))
                    return await FetchContactsWithDedicatedCarLightAsync(baseUrl, eventCode, accessToken, httpClientFactory, cancellationToken, dedicatedCarGuid, rankGuid, vehicleTypeGuid);
                throw new InvalidOperationException($"GraphQL error: {errorMsg}");
            }

            var contacts = doc.GetProperty("data").GetProperty("event").GetProperty("contacts");
            int pageCount = 0;

            foreach (var contact in contacts.EnumerateArray())
            {
                pageCount++;
                var contactId = contact.TryGetProperty("id", out var cidEl) ? cidEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(contactId) || !seenContactIds.Add(contactId)) continue;

                string? rankValue = null;
                string? vehicleTypeValue = null;
                if (contact.TryGetProperty("customFields", out var cfArray) && cfArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cf in cfArray.EnumerateArray())
                    {
                        var defId = cf.TryGetProperty("definitionId", out var did) ? did.GetString() ?? "" : "";
                        if (string.Equals(defId, rankGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            if (cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null)
                                rankValue = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText().Trim('"');
                        }
                        else if (string.Equals(defId, vehicleTypeGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            if (cf.TryGetProperty("value", out var vt) && vt.ValueKind != JsonValueKind.Null)
                                vehicleTypeValue = vt.ValueKind == JsonValueKind.String ? vt.GetString() : vt.GetRawText().Trim('"');
                        }
                    }
                }

                string regTypeId = "", regTypeName = "";
                if (contact.TryGetProperty("registrations", out var regsEl) && regsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var reg in regsEl.EnumerateArray())
                    {
                        if (reg.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Object)
                        {
                            regTypeId = typeEl.TryGetProperty("id", out var tidEl) ? tidEl.GetString() ?? "" : "";
                            regTypeName = typeEl.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";
                            break;
                        }
                    }
                }

                string? country = null;
                if (contact.TryGetProperty("primaryAddress", out var addrEl) && addrEl.ValueKind == JsonValueKind.Object)
                    country = addrEl.TryGetProperty("country", out var cEl) && cEl.ValueKind != JsonValueKind.Null ? cEl.GetString() : null;

                string? photoUrl = null;
                if (contact.TryGetProperty("photo", out var photoEl) && photoEl.ValueKind == JsonValueKind.Object)
                    photoUrl = photoEl.TryGetProperty("url", out var urlEl) && urlEl.ValueKind != JsonValueKind.Null ? urlEl.GetString() : null;

                fetched.Add(new EventsAirSyncContactDto(
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

            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        return fetched;
    }

    private static async Task<List<EventsAirSyncContactDto>> FetchContactsWithDedicatedCarLightAsync(
        string baseUrl, string eventCode, string accessToken,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken,
        string? dedicatedCarGuid = null, string? rankGuid = null, string? vehicleTypeGuid = null)
    {
        dedicatedCarGuid ??= DedicatedCarFieldGuid;
        rankGuid ??= RankFieldGuid;
        vehicleTypeGuid ??= VehicleTypeFieldGuid;
        var fetched = new List<EventsAirSyncContactDto>();
        var seenContactIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        int offset = 0;
        const int pageSize = 50;

        while (true)
        {
            var graphqlQuery = $@"{{
              event(id: ""{eventCode}"") {{
                contacts(input: {{ contactFilter: {{ customFields: {{ checkboxCustomFieldFilters: [{{ definitionId: ""{dedicatedCarGuid}"", isChecked: true }}] }} }} }}, offset: {offset}, limit: {pageSize}) {{
                  id firstName lastName title jobTitle organizationName primaryEmail
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

                fetched.Add(new EventsAirSyncContactDto(
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
                    RankValue: null,
                    VehicleTypeValue: null
                ));
            }

            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        // Fetch Rank and VehicleType values separately
        if (fetched.Count > 0)
        {
            var contactIds = fetched.Select(c => c.ContactId).ToList();
            var rankValues = await FetchCustomFieldValuesAsync(baseUrl, eventCode, accessToken, rankGuid, contactIds, httpClientFactory, cancellationToken);
            var vehicleTypeValues = await FetchCustomFieldValuesAsync(baseUrl, eventCode, accessToken, vehicleTypeGuid, contactIds, httpClientFactory, cancellationToken);
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

    // ─── Fetch travel bookings ────────────────────────────────────────────────

    public static async Task<List<EventsAirTravelDto>> FetchTravelBookingsAsync(
        string baseUrl, string eventCode, string accessToken,
        IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var result = new List<EventsAirTravelDto>();
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        const int pageSize = 100;
        int offset = 0;

        while (true)
        {
            var queryBody = JsonSerializer.Serialize(new
            {
                query = $"{{ event(id: \"{eventCode}\") {{ travelBookings(input: {{}}, limit: {pageSize}, offset: {offset}) {{ id contact {{ id }} travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} class bookingNotes comment }} }} }}"
            });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(req, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) break;

            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            if (doc.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0) break;

            int pageCount = 0;
            if (doc.TryGetProperty("data", out var data) &&
                data.TryGetProperty("event", out var eventObj) &&
                eventObj.TryGetProperty("travelBookings", out var bookings))
            {
                foreach (var booking in bookings.EnumerateArray())
                {
                    result.Add(new EventsAirTravelDto
                    {
                        Id = booking.GetProperty("id").GetString() ?? string.Empty,
                        ContactId = booking.TryGetProperty("contact", out var c) && c.ValueKind == JsonValueKind.Object
                            ? (c.TryGetProperty("id", out var cid) ? cid.GetString() ?? "" : "") : "",
                        TravelTypeName = booking.TryGetProperty("travelType", out var tt) && tt.ValueKind == JsonValueKind.Object
                            ? (tt.TryGetProperty("name", out var ttn) ? ttn.GetString() : null) : null,
                        FlightNumber = booking.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetString() : null,
                        CarrierName = booking.TryGetProperty("carrier", out var cr) && cr.ValueKind == JsonValueKind.Object
                            ? (cr.TryGetProperty("name", out var crn) ? crn.GetString() : null) : null,
                        ArrivalDate = booking.TryGetProperty("arrivalDate", out var ad) && ad.ValueKind != JsonValueKind.Null ? ad.GetString() : null,
                        DepartureDate = booking.TryGetProperty("departureDate", out var dd) && dd.ValueKind != JsonValueKind.Null ? dd.GetString() : null,
                        Eta = booking.TryGetProperty("eta", out var eta) && eta.ValueKind != JsonValueKind.Null ? eta.GetString() : null,
                        Etd = booking.TryGetProperty("etd", out var etd) && etd.ValueKind != JsonValueKind.Null ? etd.GetString() : null,
                        DeparturePortName = booking.TryGetProperty("departurePort", out var dp) && dp.ValueKind == JsonValueKind.Object
                            ? (dp.TryGetProperty("name", out var dpn) ? dpn.GetString() : null) : null,
                        ArrivalPortName = booking.TryGetProperty("arrivalPort", out var ap) && ap.ValueKind == JsonValueKind.Object
                            ? (ap.TryGetProperty("name", out var apn) ? apn.GetString() : null) : null,
                        SeatClass = booking.TryGetProperty("class", out var sc) && sc.ValueKind != JsonValueKind.Null ? sc.GetString() : null,
                        BookingNotes = booking.TryGetProperty("bookingNotes", out var bn) && bn.ValueKind != JsonValueKind.Null ? bn.GetString() : null,
                        Comment = booking.TryGetProperty("comment", out var cmt) && cmt.ValueKind != JsonValueKind.Null ? cmt.GetString() : null
                    });
                    pageCount++;
                }
            }

            if (pageCount < pageSize) break;
            offset += pageSize;
        }

        return result;
    }

    // ─── Fetch travel bookings per-contact (batched aliases) ─────────────────
    // Used when the global travelBookings query hangs (e.g. 2026 event).
    // Batches contactIds 10 at a time using GraphQL field aliases.
    public static async Task<List<EventsAirTravelDto>> FetchTravelBookingsByContactsAsync(
        string baseUrl, string eventCode, string accessToken,
        IHttpClientFactory httpClientFactory,
        IEnumerable<string> contactIds,
        CancellationToken cancellationToken)
    {
        var result = new List<EventsAirTravelDto>();
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        const int batchSize = 10;
        var idList = contactIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        for (int i = 0; i < idList.Count; i += batchSize)
        {
            var batch = idList.Skip(i).Take(batchSize).ToList();
            // Build a single GraphQL query with one alias per contact
            var contactFragments = string.Join(" ",
                batch.Select((id, idx) =>
                    $"c{idx}: contact(id: \"{id}\") {{ id travelBookings {{ id travelType {{ name }} flightNumber carrier {{ name }} arrivalDate departureDate eta etd departurePort {{ name }} arrivalPort {{ name }} class bookingNotes comment }} }}"));
            var queryBody = JsonSerializer.Serialize(new
            {
                query = $"{{ event(id: \"{eventCode}\") {{ {contactFragments} }} }}"
            });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
            };
            HttpResponseMessage response;
            string json;
            try
            {
                response = await client.SendAsync(req, cancellationToken);
                json = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) { Console.WriteLine($"[TRAVEL-BATCH] HTTP exception for batch {i/batchSize}: {ex.Message}"); continue; }
            if (!response.IsSuccessStatusCode) { Console.WriteLine($"[TRAVEL-BATCH] HTTP {(int)response.StatusCode} for batch {i/batchSize}: {json[..Math.Min(json.Length,300)]}"); continue; }
            JsonElement doc;
            try { doc = JsonSerializer.Deserialize<JsonElement>(json); }
            catch (Exception ex) { Console.WriteLine($"[TRAVEL-BATCH] JSON parse error for batch {i/batchSize}: {ex.Message}"); continue; }
            if (!doc.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("event", out var eventObj))
            {
                Console.WriteLine($"[TRAVEL-BATCH] No data.event for batch {i/batchSize}: {json[..Math.Min(json.Length,500)]}");
                continue;
            }
            // Each alias is c0, c1, ... cN
            for (int idx = 0; idx < batch.Count; idx++)
            {
                var aliasKey = $"c{idx}";
                var contactId = batch[idx];
                if (!eventObj.TryGetProperty(aliasKey, out var contactObj) ||
                    contactObj.ValueKind != JsonValueKind.Object) continue;
                if (!contactObj.TryGetProperty("travelBookings", out var bookings)) continue;
                foreach (var booking in bookings.EnumerateArray())
                {
                    result.Add(new EventsAirTravelDto
                    {
                        Id = booking.TryGetProperty("id", out var bid) ? bid.GetString() ?? "" : "",
                        ContactId = contactId,
                        TravelTypeName = booking.TryGetProperty("travelType", out var tt) && tt.ValueKind == JsonValueKind.Object
                            ? (tt.TryGetProperty("name", out var ttn) ? ttn.GetString() : null) : null,
                        FlightNumber = booking.TryGetProperty("flightNumber", out var fn) && fn.ValueKind != JsonValueKind.Null ? fn.GetString() : null,
                        CarrierName = booking.TryGetProperty("carrier", out var cr) && cr.ValueKind == JsonValueKind.Object
                            ? (cr.TryGetProperty("name", out var crn) ? crn.GetString() : null) : null,
                        ArrivalDate = booking.TryGetProperty("arrivalDate", out var ad) && ad.ValueKind != JsonValueKind.Null ? ad.GetString() : null,
                        DepartureDate = booking.TryGetProperty("departureDate", out var dd) && dd.ValueKind != JsonValueKind.Null ? dd.GetString() : null,
                        Eta = booking.TryGetProperty("eta", out var eta) && eta.ValueKind != JsonValueKind.Null ? eta.GetString() : null,
                        Etd = booking.TryGetProperty("etd", out var etd) && etd.ValueKind != JsonValueKind.Null ? etd.GetString() : null,
                        DeparturePortName = booking.TryGetProperty("departurePort", out var dp) && dp.ValueKind == JsonValueKind.Object
                            ? (dp.TryGetProperty("name", out var dpn) ? dpn.GetString() : null) : null,
                        ArrivalPortName = booking.TryGetProperty("arrivalPort", out var ap) && ap.ValueKind == JsonValueKind.Object
                            ? (ap.TryGetProperty("name", out var apn) ? apn.GetString() : null) : null,
                        SeatClass = booking.TryGetProperty("class", out var sc) && sc.ValueKind != JsonValueKind.Null ? sc.GetString() : null,
                        BookingNotes = booking.TryGetProperty("bookingNotes", out var bn) && bn.ValueKind != JsonValueKind.Null ? bn.GetString() : null,
                        Comment = booking.TryGetProperty("comment", out var cmt) && cmt.ValueKind != JsonValueKind.Null ? cmt.GetString() : null
                    });
                }
            }
        }
        return result;
    }

    // ─── Process travel bookings into DB (shared by both sync paths) ──────────────

    /// <summary>
    /// Core flight+booking processing logic shared by both sync paths:
    /// - GuestsController background guest sync
    /// - EventsAirController manual travel sync
    ///
    /// Rules enforced here (single source of truth):
    /// 1. Flight key = FlightNumber + raw ArrivalDate/DepartureDate (NOT computed datetime)
    ///    → same flight on different dates = separate rows
    /// 2. Skip booking if date is missing — never fall back to a placeholder date
    /// 3. ScheduledArrival/ScheduledDeparture are set ONCE on row creation and never
    ///    overwritten by subsequent guests on the same flight
    /// 4. History is saved when a guest's flight changes (rebook)
    /// </summary>
    public static async Task<TravelSyncResult> ProcessTravelBookingsAsync(
        IAppDbContext db,
        List<EventsAirTravelDto> travelBookings,
        CancellationToken cancellationToken = default)
    {
        var result = new TravelSyncResult();

        // Bulk-load active guests with their travel bookings and linked flights
        var guestsByContactId = await db.Guests
            .Where(g => g.IsActive)
            .Include(g => g.TravelBookings)
                .ThenInclude(tb => tb.Flight)
            .ToDictionaryAsync(g => g.EventsAirContactId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Bulk-load all existing flights keyed by (FlightNumber|Date).
        // Same flight number on different dates = separate rows.
        // Same flight number + same date = one row (updated in place).
        var flightsByKey = await db.Flights
            .ToDictionaryAsync(
                f => $"{f.FlightNumber}|{f.ScheduledArrival.Date:yyyy-MM-dd}",
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        foreach (var tb in travelBookings)
        {
            try
            {
                if (string.IsNullOrEmpty(tb.FlightNumber))   { result.SkippedNoFlight++;   continue; }
                if (string.IsNullOrEmpty(tb.ContactId))      { result.SkippedNoContact++;  continue; }
                if (!guestsByContactId.TryGetValue(tb.ContactId, out var guest)) { result.SkippedNoGuest++; continue; }

                bool isArrival = tb.TravelTypeName?.Contains("Arrival", StringComparison.OrdinalIgnoreCase) ?? true;

                // Parse scheduled times. EventsAir provides local Jeddah time (UTC+3) but we
                // store them as DateTimeKind.Utc WITHOUT subtracting the offset so that the
                // stored value is numerically identical to what EventsAir shows (e.g. 13:00
                // stored as 13:00Z). The frontend strips the Z and displays 13:00.
                DateTime? scheduledArrival = null, scheduledDeparture = null;
                if (isArrival && !string.IsNullOrEmpty(tb.ArrivalDate) && DateTime.TryParse(tb.ArrivalDate, out var arrDate))
                {
                    var local = !string.IsNullOrEmpty(tb.Eta) && TimeSpan.TryParse(tb.Eta, out var eta)
                        ? arrDate.Add(eta) : arrDate;
                    scheduledArrival = DateTime.SpecifyKind(local, DateTimeKind.Utc);
                }
                if (!isArrival && !string.IsNullOrEmpty(tb.DepartureDate) && DateTime.TryParse(tb.DepartureDate, out var depDate))
                {
                    var local = !string.IsNullOrEmpty(tb.Etd) && TimeSpan.TryParse(tb.Etd, out var etd)
                        ? depDate.Add(etd) : depDate;
                    scheduledDeparture = DateTime.SpecifyKind(local, DateTimeKind.Utc);
                }

                // Build the flight key from the RAW date string (not the computed datetime).
                // This prevents the 2026-01-01 fallback from merging all date-less guests.
                var rawDateStr = isArrival ? tb.ArrivalDate : tb.DepartureDate;
                if (string.IsNullOrEmpty(rawDateStr) || !DateTime.TryParse(rawDateStr, out var parsedDate))
                {
                    result.SkippedNoFlight++;
                    continue;
                }
                var flightKey = $"{tb.FlightNumber}|{parsedDate.Date:yyyy-MM-dd}";

                if (!flightsByKey.TryGetValue(flightKey, out var flight))
                {
                    // First guest on this flight+date — create the row
                    flight = new Flight
                    {
                        FlightNumber       = tb.FlightNumber,
                        AirlineName        = tb.CarrierName ?? "Unknown",
                        ScheduledArrival   = scheduledArrival   ?? DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc),
                        ScheduledDeparture = scheduledDeparture ?? DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc),
                        ArrivalPortName    = tb.ArrivalPortName,
                        ArrivalPortIataCode   = tb.ArrivalPortCode,
                        DeparturePortName  = tb.DeparturePortName,
                        DeparturePortIataCode = tb.DeparturePortCode,
                        Status             = FlightStatus.Scheduled
                    };
                    db.Flights.Add(flight);
                    flightsByKey[flightKey] = flight;
                }
                else
                {
                    // Subsequent guest on same flight+date — update non-date fields ONLY.
                    // ScheduledArrival/ScheduledDeparture must NOT be overwritten: a later
                    // guest's value would corrupt the date for all guests on this flight.
                    if (!string.IsNullOrEmpty(tb.ArrivalPortName))    flight.ArrivalPortName    = tb.ArrivalPortName;
                    if (!string.IsNullOrEmpty(tb.ArrivalPortCode))    flight.ArrivalPortIataCode   = tb.ArrivalPortCode;
                    if (!string.IsNullOrEmpty(tb.DeparturePortName))  flight.DeparturePortName  = tb.DeparturePortName;
                    if (!string.IsNullOrEmpty(tb.DeparturePortCode))  flight.DeparturePortIataCode = tb.DeparturePortCode;
                    if (!string.IsNullOrEmpty(tb.CarrierName))        flight.AirlineName        = tb.CarrierName;
                    // ActualTerminal, ActualGate, Status are AviationStack-owned — never touch here
                }

                var notes = tb.BookingNotes ?? tb.Comment;
                var existingBooking = guest.TravelBookings.FirstOrDefault(b => b.IsArrival == isArrival);

                if (existingBooking == null)
                {
                    db.TravelBookings.Add(new TravelBooking
                    {
                        GuestId      = guest.Id,
                        Flight       = flight,   // navigation property — EF resolves FK on SaveChanges
                        IsArrival    = isArrival,
                        SeatClass    = tb.SeatClass,
                        BookingNotes = notes,
                        LastSyncedAt = DateTime.UtcNow
                    });
                    result.SavedNew++;
                }
                else if (existingBooking.FlightId != flight.Id)
                {
                    // Flight changed — save history then update the booking
                    db.TravelBookingHistories.Add(new TravelBookingHistory
                    {
                        TravelBookingId          = existingBooking.Id,
                        GuestId                  = guest.Id,
                        PreviousFlightNumber     = existingBooking.Flight?.FlightNumber ?? "Unknown",
                        PreviousAirlineName      = existingBooking.Flight?.AirlineName,
                        PreviousScheduledArrival = existingBooking.Flight?.ScheduledArrival,
                        PreviousScheduledDeparture = existingBooking.Flight?.ScheduledDeparture,
                        PreviousDeparturePort    = existingBooking.Flight?.DeparturePortName,
                        PreviousArrivalPort      = existingBooking.Flight?.ArrivalPortName,
                        PreviousSeatClass        = existingBooking.SeatClass,
                        ChangedAt                = DateTime.UtcNow
                    });
                    existingBooking.PreviousFlightNumber  = existingBooking.Flight?.FlightNumber;
                    existingBooking.Flight                = flight;
                    existingBooking.SeatClass             = tb.SeatClass;
                    existingBooking.BookingNotes          = notes;
                    existingBooking.ChangedSinceLastView  = true;
                    existingBooking.ChangedAt             = DateTime.UtcNow;
                    existingBooking.LastSyncedAt          = DateTime.UtcNow;
                    result.Rebooked++;
                }
                else
                {
                    // Same flight — just refresh mutable fields
                    existingBooking.SeatClass    = tb.SeatClass;
                    existingBooking.BookingNotes = notes;
                    existingBooking.LastSyncedAt = DateTime.UtcNow;
                    result.UpdatedExisting++;
                }
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                if (result.Errors.Count < 5)
                    result.Errors.Add($"ContactId={tb.ContactId} Flight={tb.FlightNumber}: {ex.Message}");
            }
        }

        return result;
    }

    // ─── Fetch custom field values ──────────────────────────────────────────────────

    public static async Task<Dictionary<string, string>> FetchCustomFieldValuesAsync(
        string baseUrl, string eventCode, string accessToken, string fieldDefinitionId,
        IEnumerable<string> contactIds, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var result = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allContactIds = contactIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        var client = httpClientFactory.CreateClient();
        const int concurrency = 15;

        for (int i = 0; i < allContactIds.Count; i += concurrency)
        {
            var batch = allContactIds.Skip(i).Take(concurrency).ToList();
            var tasks = batch.Select(async contactId =>
            {
                try
                {
                    var queryBody = JsonSerializer.Serialize(new { query = $"{{ event(id: \"{eventCode}\") {{ contact(id: \"{contactId}\") {{ id customFields {{ definitionId value }} }} }} }}" });
                    var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/graphql")
                    {
                        Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                        Content = new StringContent(queryBody, Encoding.UTF8, "application/json")
                    };
                    var response = await client.SendAsync(req, cancellationToken);
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode) return;
                    var doc = JsonSerializer.Deserialize<JsonElement>(json);
                    if (doc.TryGetProperty("errors", out _) || !doc.TryGetProperty("data", out var data)) return;
                    var contactEl = data.GetProperty("event").GetProperty("contact");
                    if (contactEl.ValueKind == JsonValueKind.Null) return;
                    foreach (var cf in contactEl.GetProperty("customFields").EnumerateArray())
                    {
                        var defId = cf.GetProperty("definitionId").GetString() ?? "";
                        if (!string.Equals(defId, fieldDefinitionId, StringComparison.OrdinalIgnoreCase)) continue;
                        if (cf.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null)
                        {
                            var val = v.ValueKind == JsonValueKind.Object
                                ? (v.TryGetProperty("value", out var nv) ? nv.GetString() : v.TryGetProperty("text", out var tv) ? tv.GetString() : v.GetRawText())
                                : v.GetRawText();
                            if (!string.IsNullOrEmpty(val)) result[contactId] = val.Trim('"');
                        }
                    }
                }
                catch { }
            });
            await Task.WhenAll(tasks);
        }

        return new Dictionary<string, string>(result, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Contact DTO used internally by the sync helpers (includes RankValue, JobTitle, etc.).
/// Separate from the Application-layer EventsAirContactDto which has different fields.
/// </summary>
public record EventsAirSyncContactDto(
    string ContactId, string FirstName, string LastName, string? Title,
    string? JobTitle, string? OrganizationName, string? PrimaryEmail,
    string RegistrationTypeId, string RegistrationTypeName,
    string? Country = null, string? PhotoUrl = null, string? RankValue = null, string? VehicleTypeValue = null);

/// <summary>
/// Result counters returned by <see cref="EventsAirSyncHelpers.ProcessTravelBookingsAsync"/>.
/// </summary>
public class TravelSyncResult
{
    public int SavedNew         { get; set; }
    public int UpdatedExisting  { get; set; }
    public int Rebooked         { get; set; }
    public int SkippedNoFlight  { get; set; }
    public int SkippedNoContact { get; set; }
    public int SkippedNoGuest   { get; set; }
    public int ErrorCount       { get; set; }
    public List<string> Errors  { get; } = new();
}
