# Lessons Learned — Annual Meetings Hospitality (IsDB) UAT Sprint

**Environment:** UAT (`https://annualmeetings-uat.up.railway.app`)
**Branch:** `uat`
**Period:** June 2026
**Stack:** .NET 8 / C#, React/TypeScript, PostgreSQL, Railway

---

## Issue 1 — AviationStack Returning Wrong Flight Instance

**Problem:** AviationStack was returning the wrong daily flight instance (e.g., yesterday's TK334 instead of today's) because the API call did not include a `flight_date` parameter.

**Root cause:** The `AviationstackClient.cs` call omitted `flight_date`, so AviationStack defaulted to the most recently completed flight — which could be from a previous day for a recurring flight number.

**Fix:** Pass `flight_date` explicitly on every AviationStack request.

**Lesson:** For daily recurring flights, AviationStack's default behaviour is to return the most recently completed instance, not the next scheduled one. Always supply `flight_date` when tracking a specific departure date.

---

## Issue 2 — EventsAir Normalization: Inline vs Shared Helper

**Problem:** The `EventsAirController` (webhook path) had its own inline travel booking processing block that diverged from the `GuestsController` (manual sync path). The two paths applied different deduplication and normalization rules, causing inconsistent data depending on which path triggered the sync.

**Root cause:** Copy-paste duplication — the webhook path was written independently from the manual sync path and was never updated when the manual sync logic evolved.

**Fix:** Replaced the inline block in `EventsAirController` with a call to the shared `ProcessTravelBookingsAsync` helper in `EventsAirSyncHelpers.cs`. This is now the single source of truth for all flight and booking processing.

**Lesson:** Any logic that must behave identically across multiple entry points (webhook, manual trigger, background job) must live in a shared helper from the start. Inline duplication will diverge silently over time.

---

## Issue 3 — Flight Time Conflict Alerts Showed GUIDs Instead of Guest Names

**Problem:** When two guests on the same flight had different scheduled arrival times, the conflict alert message contained raw `ContactId` GUIDs (e.g., `5635126F-...`) instead of readable guest names.

**Root cause:** The conflict detection code used the `ContactId` string as the identifier in the alert title, because the guest name was not available at the point where the alert was raised.

**Fix (two-step):**
1. Pre-load a `firstGuestNameByFlightKey` dictionary from existing `TravelBookings` at sync start, so flights already in the DB have a real name associated.
2. Populate the dictionary with the current guest's full name as each booking is processed, so the second guest on the same flight sees the first guest's name.

**Lesson:** When raising alerts that reference entities, always resolve human-readable identifiers (names) at the point of alert creation. Storing raw FK values in user-facing messages is a UX defect that is easy to miss in testing if you only look at the alert count, not the content.

---

## Issue 4 — Actual Arrival Time Not Displayed in Airport Page

**Problem:** The Airport Reception dashboard showed scheduled arrival time but not the actual arrival time recorded by AviationStack, even though the data existed in the database.

**Root cause:** `ActualArrival` was present on the `Flight` entity but was not mapped in `ReceptionSummaryDto`, `GetReceptionSummaryQuery`, or the frontend component.

**Fix:** Added `ActualArrival` to the DTO, query handler mapping, and the `ReceptionDashboard.tsx` frontend component.

**Lesson:** When adding a new field to an entity, audit all DTOs and query handlers that project from that entity. Fields silently absent from a DTO will never surface in the UI regardless of what the DB contains.

---

## Issue 5 — AviationStack Health Indicator Always Showed Green

**Problem:** The AviationStack health indicator in the Airport page always displayed "Live · AviationStack" in green, even when the API key was not configured, tracking was disabled, or the last sync had failed.

**Root cause:** The frontend bundle contained hardcoded logic that always rendered the green "Live" state. The Airport page has no `.tsx` source file in the repo — it is compiled into the shared bundle — so the fix required direct bundle surgery.

**Fix:** Located the relevant logic in `index-carclass-hist-v24.js`, rewrote the indicator function to read the actual sync status from the API response (`isConfigured`, `isEnabled`, `lastSyncStatus`), and deployed as `index-carclass-hist-v25.js` with the `index.html` reference updated.

**Lesson:** Bundle surgery is a high-risk, low-visibility technique. It works but leaves no readable source. For any Airport page changes going forward, the original `.tsx` source should be recovered or rewritten so future changes can be made normally. The version suffix on the bundle filename (`v24` → `v25`) is essential for cache-busting.

---

## Issue 6 — AviationStack 401 Errors Logged as Success

**Problem:** When the AviationStack API key was invalid or expired, the sync service logged a `Success` status instead of `Failed`, making it impossible to detect a broken API key from the sync log.

**Root cause:** `AviationstackClient.cs` did not check for HTTP 401/403 responses or for the `{"error": {...}}` JSON body that AviationStack returns for auth failures. Both paths fell through to the normal response parsing, which returned an empty result — indistinguishable from "no flights found."

**Fix:** Added explicit checks for HTTP 401/403 (throws `InvalidOperationException`) and for the `error` key in the JSON body. Both catch blocks re-throw auth errors so `FlightTrackerSyncService` logs a `Failed` status.

**Lesson:** Third-party APIs often return HTTP 200 with an error payload for auth failures. Always inspect the response body for error indicators, not just the HTTP status code.

---

## Issue 7 — Performance Optimization Regression: Batch Size 25 Silently Broke Travel Sync

**Problem:** After increasing the GraphQL batch size from 10 to 25 (to reduce API calls by ~60%), all subsequent syncs returned `travelFetched: 0` — EventsAir returned empty `travelBookings` arrays for every contact. No error was raised; the sync completed with `Success` status.

**Root cause:** EventsAir's GraphQL engine has an undocumented query complexity limit. A query with 25 contact aliases × multiple nested fields exceeded this limit. Rather than returning an error, EventsAir silently returned empty data for all aliases. The `[TRAVEL-BATCH]` console log in the code correctly printed "No data.event" for each batch, but this was only visible in Railway logs, not in the sync status response.

**Timeline:** The regression was introduced at 02:25 and went undetected until ~06:27 (about 4 hours), during which time 6 syncs ran with `Travel: 0` before being noticed.

**Fix:** Reverted `batchSize` from 25 to 10.

**Lessons:**
- **Silent data loss is worse than an error.** A third-party API returning empty data on overload is far harder to detect than an HTTP 500. The sync log showed `Success` with `Travel: 0`, which looks identical to a legitimate "no changes" sync.
- **Always verify `travelFetched` (not just `travelUpdated`) after a sync.** A sync that fetches 0 bookings and updates 0 is suspicious — it should be treated as a warning, not a success, when the expected count is ~178.
- **Test performance optimizations under realistic load before deploying.** The batch size change was not tested against EventsAir's actual complexity limits.
- **Add a guard:** If `travelFetched == 0` but there are active guests with existing bookings, log a warning or set sync status to `Warning` rather than `Success`.

---

## Issue 8 — Flight Date Change Not Persisted After EventsAir Update

**Problem:** When a guest's flight date was changed in EventsAir (Abdulrahman Murad: TK334 Jun 4 → Jun 5), subsequent syncs continued to show Jun 4. The booking was not updated.

**Root cause:** The `flightsByKey` dictionary is keyed by `{FlightNumber}|{Date:yyyy-MM-dd}`. When the date changes, the new key (`TK334|2026-06-05`) is not found in the dictionary, so a new flight row is created. However, the existing booking's `FlightId` still pointed to the old Jun 4 row. The rebook step compares `existingBooking.FlightId != flight.Id` — but because the new row was created in the same sync run (not yet saved), EF Core's change tracking should have handled the FK update. The actual failure was that the rebook was running but the new flight row was not being persisted correctly in some scenarios.

**Fix:** Before the `flightsByKey` lookup, detect if the guest's existing booking has a different date from the incoming EventsAir date:
- **Solo flight** (only this guest on the old row): update `ScheduledArrival` in-place and re-key the dictionary. This preserves AviationStack live-tracking data on the existing row.
- **Shared flight** (multiple guests on the old row): leave the old row intact; the new key creates a new row and the existing rebook step relinks this guest's booking.

**Lesson:** Dictionary-keyed deduplication is fragile when the key itself can change. Any time a key field (flight number, date) changes in the source system, the dictionary must be explicitly re-keyed — it will not self-correct. The fix must happen *before* the lookup, not after.

---

## General Lessons

| Theme | Lesson |
|-------|--------|
| **EventsAir API reliability** | EventsAir returns HTTP 500 intermittently and silently returns empty data under query complexity load. Both must be handled explicitly. |
| **Silent failures** | A `Success` sync status with `travelFetched: 0` is not necessarily correct. Consider adding a `Warning` state for anomalous-but-not-errored conditions. |
| **Single source of truth** | All travel booking processing must go through `ProcessTravelBookingsAsync`. Any new sync entry point must call this helper, never reimplement inline. |
| **Bundle surgery** | Effective as a one-off fix but leaves no maintainable source. Limit to emergency fixes; recover source code for any page that requires repeated changes. |
| **DTO completeness** | When adding fields to entities, always trace the full projection path: entity → query handler → DTO → API response → frontend component. |
| **Third-party API contracts** | Do not assume HTTP 200 = success. Always check for error payloads in the response body, especially for auth-related failures. |

---

## Commit Reference

| Commit | Description |
|--------|-------------|
| `7184825` | fix: pass `flight_date` to AviationStack to prevent wrong flight instance |
| `a3aa4e9` | fix: replace inline EventsAir travel processing with shared `ProcessTravelBookingsAsync` |
| `6fab9a0` | feat: flight time conflict alerts with both guest ContactIds and notifications |
| `42d81a9` | fix: pre-load firstContactId from existing TravelBookings for conflict alerts |
| `0766c2d` | fix: show guest full names instead of ContactId GUIDs in conflict messages |
| `4c1056e` | feat: display actual arrival time (AviationStack) in Airport page |
| `d884b6f` | feat: real AviationStack health status indicator (not always green) |
| `114d8cd` | fix: throw on 401/403 from AviationStack so invalid key logs as Failed |
| `6fc56b0` | chore: tag `v3.0.0-uat-stable` pre-release checkpoint |
| `610af1d` | perf: batch size 10→25, merge Pass1+2 saves, cache field mappings 1h |
| `4172527` | fix: detect flight date changes in EventsAir and update flight row in-place |
| `c6fad0a` | fix: revert travel booking batch size 25→10 (EventsAir silent empty response) |
