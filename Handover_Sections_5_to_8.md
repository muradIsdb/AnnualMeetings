## 5. Database Documentation

### 5.1 Entity Relationship Diagram (ERD)
*(Note: Mermaid diagrams will be generated in Section 10)*
The core schema revolves around the `Guest` entity, which acts as the central hub connecting `TravelBooking`, `VehicleAssignment`, `GuestStatusHistory`, and `ChecklistCompletion`. The `Vehicle` entity connects to `CarClass` and `Driver`, and maintains its own `VehicleStatusHistory`.

### 5.2 Table Descriptions
- **Guests:** Stores VIP contact details, hotel info, registration types, and current journey statuses (`InboundStatus`, `OutboundStatus`).
- **Flights:** A shared repository of flight schedules and real-time tracking data from Aviationstack.
- **TravelBookings:** Links a `Guest` to a `Flight`, containing guest-specific booking details (seat class, PNR, terminal). Includes a `History` table for tracking itinerary changes.
- **Vehicles & Drivers:** Fleet management tables. Vehicles have a `Status` (Available, Assigned, OutOfService, NotProvided) and are linked to `CarClasses`.
- **VehicleAssignments:** Join table mapping a `Guest` to a `Vehicle` with timestamps, staff IDs, and assignment types (DropOff vs. Dedicated).
- **StaffUsers & StaffUserRoles:** Authentication and RBAC. Supports multiple roles per user.
- **SyncAlerts:** Records discrepancies found during EventsAir sync (e.g., CarClassMismatch, GuestRemoved) for manual resolution.
- **SystemLogs:** Persistent storage for application errors, sync summaries, and audit trails.
- **Notifications & NotificationReads:** Internal messaging system with read-receipt tracking per user.

### 5.3 Relationships
- **Guest (1) to TravelBookings (Many):** A guest can have multiple bookings (typically one arrival, one departure).
- **Flight (1) to TravelBookings (Many):** Multiple guests can be on the same flight.
- **Vehicle (1) to VehicleAssignments (Many):** A vehicle can be assigned to multiple guests over time, but only one active assignment at a time.
- **Guest (1) to SyncAlerts (Many):** Alerts are tied to the guest they affect.

### 5.4 Indexes and Performance Considerations
- **Unique Indexes:** `EventsAirContactId` on `Guests`, `Email` on `DepartureRequests`, `RegistrationTypeName` on `CarClassRules`.
- **Composite Indexes:** `(IsResolved, DetectedAt)` on `SyncAlerts` for fast dashboard filtering. `(Severity, OccurredAt)` on `SystemLogs`.
- **Foreign Keys:** Strict referential integrity. Deleting a guest cascades to assignments and bookings, but sets `GuestId` to null on `SyncAlerts` to preserve the audit trail.

---

## 6. API Documentation

### 6.1 All Endpoints (Summary)
The API is divided into logical controllers matching the frontend feature modules:
- **Auth:** `/api/auth/login`, `/api/auth/change-password`
- **Guests:** `/api/guests`, `/api/guests/{id}/inbound-status`, `/api/guests/sync-from-eventsair`
- **Fleet/Vehicles:** `/api/vehicles`, `/api/vehicles/assign`, `/api/car-classes`
- **Dashboard:** `/api/dashboard/summary`, `/api/dashboard/hotel-summary`
- **Alerts/Notifications:** `/api/transport-actions` (SyncAlerts), `/api/notifications`
- **Settings:** `/api/settings/config`, `/api/eventsair/config`

### 6.2 Authentication Methods
- **Method:** JWT (JSON Web Token) passed in the `Authorization: Bearer <token>` header.
- **Lifespan:** Access tokens are valid for 8 hours. Refresh tokens are valid for 7 days.
- **Roles:** Encoded as `ClaimTypes.Role` in the JWT payload.

### 6.3 Request and Response Examples
**Example: Update Inbound Status**
`POST /api/guests/{id}/inbound-status`
*Request:*
```json
{
  "status": 3, // VehicleAssigned
  "notes": "Driver dispatched"
}
```
*Response (200 OK):*
```json
{
  "id": "1234-5678-...",
  "inboundStatus": 3,
  "statusHistory": [...]
}
```

### 6.4 Error Handling Conventions
All API errors return a standard JSON structure via the `GlobalExceptionMiddleware`:
```json
{
  "message": "An internal server error occurred. Please check the system logs for details.",
  "error": "Detailed exception message (UAT/Dev only)"
}
```
Validation errors (FluentValidation) return a 400 Bad Request with a `errors` dictionary detailing field-specific validation failures.

---

## 7. Security Assessment

### 7.1 Authentication Review
- **Strengths:** Uses standard JWT with BCrypt password hashing. Refresh tokens are securely generated using `RandomNumberGenerator`.
- **Observations:** Refresh tokens are stored in plain text in the DB. While acceptable, hashing them would provide defense-in-depth against DB leaks.

### 7.2 Authorization Review
- **Strengths:** Granular `[Authorize(Roles = "...")]` attributes on almost all endpoints. The `ActiveUserFilter` effectively mitigates the "stale JWT" problem by checking the DB (via a short-lived cache) on every request to ensure the user hasn't been deactivated.
- **Observations:** Some endpoints (e.g., `/api/health`) are `[AllowAnonymous]`, which is appropriate. The Departure Request endpoints correctly use a GUID `ManageToken` for unauthenticated user access to their own records.

### 7.3 Input Validation Review
- **Strengths:** FluentValidation is used extensively across DTOs and Commands. The `SlugifyParameterTransformer` ensures consistent routing.
- **Observations:** The `AviationstackHealthCheck` correctly handles missing API keys without crashing the application.

### 7.4 Secrets Management Review
- **Strengths:** External API credentials (EventsAir, Aviationstack) are stored in the database (`AppConfig`, `EventsAirConfig`), allowing dynamic updates without redeployment.
- **Risks:** These credentials are stored in plain text in the database. A compromised DB means compromised external API access. Consider implementing encryption-at-rest for these specific columns using a master key injected via environment variables.

### 7.5 OWASP-Related Observations
- **CORS:** Configured to allow specific origins (`AllowedOrigins` in appsettings), mitigating unauthorized cross-origin requests.
- **SQL Injection:** Mitigated entirely by the use of Entity Framework Core and parameterized queries.
- **XSS:** React frontend automatically escapes output, mitigating XSS risks.

---

## 8. Technical Debt Assessment

### 8.1 Code Smells
- **Large Controllers/Services:** `EventsAirSyncService.cs` is over 600 lines long and handles multiple complex passes (Upsert, Deactivate, Travel, Alerts). It violates the Single Responsibility Principle and should be broken down into smaller, focused sync strategies.
- **Legacy Fields:** The `Guest` entity contains legacy fields (e.g., `DedicatedCar`, `RankValue`) that are tightly coupled to specific EventsAir custom field GUIDs.

### 8.2 Areas Requiring Refactoring
- **EventsAir Sync Logic:** The sync logic relies on hardcoded GUIDs for custom fields and marketing tags. This makes the system brittle if the EventsAir event structure changes. A dynamic mapping system (partially implemented via `SyncFieldMappings`) should be fully utilized.
- **Flight Normalization:** Flight numbers are normalized in multiple places. Centralizing this logic further would prevent edge cases where Aviationstack and EventsAir formats diverge.

### 8.3 Duplicate Logic
- **Authorization Checks:** The frontend `ProtectedRoute` and the backend `[Authorize]` attributes occasionally drift in their role definitions. A shared constant library or generated types would ensure alignment.

### 8.4 Performance Bottlenecks
- **EventsAir API Limits:** The sync service pulls all contacts on every run. As the event grows, this will hit EventsAir API rate limits and memory constraints. Implementing delta syncs (fetching only records modified since `LastSyncedAt`) is critical for scale.
- **SignalR Broadcasts:** Currently, all clients join a single "airport" group. As user count grows, broadcasting every flight update to every client will consume unnecessary bandwidth. Scoping groups by role or specific flight interest would improve efficiency.

### 8.5 Scalability Limitations
- **In-Memory Cache:** The `ActiveUserFilter` and `EventsAirClient` use `IMemoryCache`. If the application is scaled horizontally across multiple Railway instances, cache invalidation will not propagate. Migrating to a distributed cache (e.g., Redis) is required for multi-instance deployments.

### 8.6 Maintainability Concerns
- **Database Seed Script:** `production_seed.sql` is manually maintained and executed on startup. While idempotent, it makes tracking configuration changes difficult. Moving this to EF Core data seeding or a dedicated migration tool would improve maintainability.
