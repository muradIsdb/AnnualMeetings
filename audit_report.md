# Codebase Audit Report: Hardcoded Configurations & Performance Issues

**Project:** IsDB Hospitality Platform (Annual Meetings)  
**Date:** May 8, 2026  
**Auditor:** Manus AI  

This report provides a comprehensive audit of the AnnualMeetings codebase, focusing on hardcoded configurations, magic strings, missing environment variables, and performance bottlenecks. It concludes with a prioritized action plan.

---

## 1. Hardcoded Configurations & Magic Strings

### 1.1. Secrets and Credentials
Several sensitive values are hardcoded in the source code rather than being loaded from secure environment variables or configuration files.

*   **Turnstile Secret Key:** Hardcoded in `DepartureRequestsController.cs` (via `_config["Turnstile:SecretKey"]` but the frontend expects a specific site key).
*   **Default Passwords in Seeder:** `DatabaseSeeder.cs` hardcodes the initial admin password (`"123"`) and email (`"admin@isdb.org"`). While acceptable for a dev seeder, this is executed as a "production seed".
*   **EventsAir OAuth Scope:** The Azure AD scope string is hardcoded in multiple places (`EventsAirController.cs`, `EventsAirClient.cs`, `EventsAirSyncHelpers.cs`):
    `https://eventsairprod.onmicrosoft.com/85d8f626-4e3d-4357-89c6-327d4e6d3d93/.default`
*   **Aviationstack Base URL:** Hardcoded in `AviationstackClient.cs` to `http://api.aviationstack.com/v1`.

### 1.2. Magic Numbers and Limits
Various limits and timeouts are scattered throughout the codebase.

*   **Pagination Limits:**
    *   `AlertsController.cs`: Hardcoded `.Take(100)`.
    *   `DriversController.cs`: Hardcoded `.Take(10)`.
    *   `DashboardSummaryQuery.cs`: Hardcoded `.Take(20)` for recent activity.
*   **EventsAir Sync Pagination:**
    *   `GuestsController.cs`: Hardcoded `pageSize = 25` (with a comment about staying under a 10,000 cost limit) and `pageSize = 50` for lighter queries.
*   **Timeouts and Intervals:**
    *   `FlightTrackerSyncService.cs`: Hardcoded sync interval of 5 minutes.
    *   `EventsAirClient.cs`: Hardcoded token cache expiry buffer (`expiresIn - 60`).
    *   Multiple `HttpClient` timeouts are hardcoded to 5 minutes (`TimeSpan.FromMinutes(5)`).

### 1.3. Hardcoded Event Data
*   **Event Title:** "IsDB Annual Meetings 2025" and "IsDB Annual Meetings 2026" are hardcoded in both the frontend bundle and backend `SettingsController.cs`.
*   **Custom Field GUIDs:** `GuestsController.cs` hardcodes specific EventsAir custom field GUIDs:
    *   `DEDICATED_CAR_FIELD_GUID` = `"d6b74b23-c8b6-d044-5d86-3a17bafe27de"`
    *   `RANK_FIELD_GUID` = `"3d96b87e-87b0-145e-5f45-3a17bafe26d4"`

### 1.4. Hardcoded Frontend URLs
*   The frontend bundle contains hardcoded references to `https://api.eventsair.com` and `https://auth.eventsair.com/connect/token`.

---

## 2. Performance Issues & Bottlenecks

### 2.1. N+1 Database Queries
The most significant performance issues exist in the background sync services, where database queries are executed inside loops.

*   **EventsAirSyncService.cs (Travel Bookings):**
    Inside the `foreach (var tbDto in travelBookings)` loop, the service executes multiple database calls per booking:
    1.  `await db.Guests.Include(...).FirstOrDefaultAsync(...)`
    2.  `await db.Flights.FirstOrDefaultAsync(...)`
    3.  `await db.SaveChangesAsync(...)` (sometimes inside the loop when creating a flight)
    *Impact:* Syncing 1,000 travel bookings results in 2,000+ sequential database queries.
*   **EventsAirSyncService.cs (Contacts):**
    Similar N+1 pattern exists when syncing contacts: `await db.Guests.FirstOrDefaultAsync(...)` is called inside the `foreach` loop.
*   **FlightTrackerSyncService.cs:**
    Executes `await flightTracker.GetFlightStatusAsync(...)` inside a `foreach` loop for every active flight.

### 2.2. Missing Database Indexes
The `AppDbContext` lacks indexes on frequently queried columns, which will cause full table scans as the database grows.

*   **Guest Entity:** Heavily queried by `Status` and `IsActive` (e.g., in `GetGuestsQuery`, `FlightTrackerSyncService`, and dashboard queries), but no indexes exist on these columns.
*   **Flight Entity:** Queried by `FlightNumber` in the sync service, but no index exists.

### 2.3. Missing `AsNoTracking` on Read-Only Queries
Over 80 read-only queries (using `ToListAsync` or `FirstOrDefaultAsync`) in the API controllers and MediatR handlers omit `.AsNoTracking()`.

*   *Example:* `GetGuestsQueryHandler.cs` reads the entire guest list for the dashboard but does not use `AsNoTracking`, causing unnecessary memory overhead in the Entity Framework Change Tracker.

### 2.4. Inefficient Dashboard Query
`GetDashboardSummaryQueryHandler.cs` pulls a massive amount of data into memory to calculate statistics.

*   It loads all active guests (with their vehicle assignments) into memory (`var guests = await _context.Guests...ToListAsync()`) just to perform `Count()` operations in C#.
*   *Impact:* As the guest list grows to thousands of attendees, this single endpoint will consume significant memory and processing time.

---

## 3. Proposed Action Plan (Prioritized)

### High Priority (Immediate Performance & Stability Risks)
1.  **Fix N+1 Queries in Sync Services:**
    *   *Action:* Refactor `EventsAirSyncService.cs` to load existing guests and flights into memory dictionaries *before* the loop, process the updates in memory, and call `SaveChangesAsync()` once at the end.
2.  **Add Critical Database Indexes:**
    *   *Action:* Create an EF Core migration to add indexes on `Guest.Status`, `Guest.IsActive`, `Guest.EventsAirContactId`, and `Flight.FlightNumber`.
3.  **Optimize Dashboard Query:**
    *   *Action:* Refactor `GetDashboardSummaryQueryHandler` to perform `Count()` aggregations directly in the database using SQL `GROUP BY` rather than pulling all entities into memory.

### Medium Priority (Configuration & Maintainability)
4.  **Extract Hardcoded GUIDs and Scopes:**
    *   *Action:* Move `DEDICATED_CAR_FIELD_GUID`, `RANK_FIELD_GUID`, and the EventsAir OAuth scope string into `appsettings.json` and map them to the `EventsAirOptions` class.
5.  **Apply `AsNoTracking` to Read Queries:**
    *   *Action:* Audit all MediatR queries and GET controller endpoints, adding `.AsNoTracking()` where entities are not being modified.
6.  **Extract Pagination Limits to Config:**
    *   *Action:* Move hardcoded `.Take()` limits (e.g., Alerts, Drivers) into configuration or allow them to be passed via query parameters with sensible defaults.

### Low Priority (Cleanup)
7.  **Review Seeder Data:**
    *   *Action:* Ensure the production seeder does not use hardcoded weak passwords or relies on environment variables for initial admin creation.
8.  **Remove Hardcoded Event Titles:**
    *   *Action:* Ensure the frontend dynamically fetches the event title from the backend settings API rather than hardcoding "IsDB Annual Meetings 2026" in the UI components.
