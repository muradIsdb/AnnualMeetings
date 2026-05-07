# Role-Based Access Control (RBAC) Feature Plan

## 1. Overview
The current system hardcodes page access and navigation visibility directly into the frontend (`App.tsx` and `AppLayout.tsx`) and backend controllers using `[Authorize(Roles = "...")]`. This limits flexibility when operational needs change during the event.

The goal is to implement an **Admin-managed Role-Based Access Control (RBAC) system**. This will allow the Administrator to dynamically configure which roles have access to which pages/modules, and the sidebar menu will automatically update to reflect these permissions.

## 2. Architecture & Data Model

To support dynamic RBAC without overcomplicating the system, we will introduce a `PagePermission` entity that maps a `UserRole` to a `PageId` (or Module).

### 2.1. Backend Changes

**New Entity: `PagePermission`**
```csharp
public class PagePermission : AuditableEntity
{
    public int Id { get; set; }
    public UserRole Role { get; set; }
    public string PageId { get; set; } // e.g., "airport.dashboard", "hotel.arrivals"
    public bool IsGranted { get; set; } // true if access is explicitly granted
}
```

**API Endpoints (`AccessControlController`)**
- `GET /api/access-control/permissions` - Returns the full matrix of roles and their permitted pages.
- `PUT /api/access-control/permissions` - Updates the permission matrix (Admin only).
- `GET /api/access-control/my-permissions` - Returns the list of `PageId`s granted to the current user's role(s).

**Authorization Middleware**
We will implement a custom Authorization Policy Provider or a global Action Filter (`[RequirePagePermission("page.id")]`) that checks the database (or a cached memory copy) to verify if the user's role is granted access to the requested endpoint's associated `PageId`.

### 2.2. Frontend Changes

**Dynamic Navigation (`AppLayout.tsx`)**
Currently, `navItems` have a hardcoded `roles` array. We will change this to use a `pageId`:
```typescript
type NavItem = { 
  to: string; 
  label: string; 
  icon: React.ElementType; 
  pageId: string; // Used for RBAC checking
  end?: boolean 
}
```
The sidebar will filter these items based on the permissions fetched from `/api/access-control/my-permissions` stored in the `authStore`.

**Dynamic Routing (`App.tsx`)**
The `<ProtectedRoute>` component will be updated to check `pageId` instead of hardcoded `allowedRoles`:
```tsx
<Route path="airport" element={
  <ProtectedRoute requiredPageId="airport.dashboard">
    <AirportDashboard />
  </ProtectedRoute>
} />
```

**New Page: Access Control Management**
A new page at `/settings/access-control` (accessible only to Admin) will display a matrix/grid where rows are Pages/Modules and columns are Roles. The Admin can toggle checkboxes to grant/revoke access.

## 3. Implementation Steps

1. **Backend Database:** Create the `PagePermission` entity and EF Core migration. Seed default permissions matching the current hardcoded logic.
2. **Backend API:** Implement the `AccessControlController` and caching mechanism for permissions.
3. **Backend Auth:** Create the custom authorization filter `[RequirePagePermission]`.
4. **Frontend Store:** Update `authStore` to fetch and store the user's granted `pageId`s upon login/app load.
5. **Frontend Routing & Nav:** Refactor `App.tsx` and `AppLayout.tsx` to use dynamic `pageId` checks instead of hardcoded roles.
6. **Frontend UI:** Build the `/settings/access-control` Admin page to manage the matrix.

## 4. Pages/Modules Inventory (The `PageId`s)

Based on current routing, here are the proposed `PageId`s:

| Module | Page | Proposed `PageId` | Default Roles |
|---|---|---|---|
| **Airport** | Dashboard | `airport.dashboard` | Airport, ControlRoom, Admin |
| **Transport** | Dashboard | `transport.dashboard` | Transport, ControlRoom, Admin |
| **Transport** | Departure Shuttle | `transport.departure_stats` | Transport, ControlRoom, Admin |
| **Control Room** | Dashboard | `controlroom.dashboard` | ControlRoom, Admin |
| **Hotel** | Dashboard | `hotel.dashboard` | Hotel, ControlRoom, Admin |
| **Hotel** | Arrivals Queue | `hotel.arrivals` | Hotel, ControlRoom, Admin |
| **Hotel** | Guest Directory | `hotel.guests` | Hotel, ControlRoom, Admin |
| **Hotel** | Management | `hotel.management` | Hotel, ControlRoom, Admin |
| **Liaison** | Dashboard | `liaison.dashboard` | Liaison, Admin |
| **Liaison** | Guest Directory | `liaison.guests` | Liaison, Admin |
| **Fleet** | Fleet Management | `fleet.management` | Transport, Admin |
| **Admin** | Staff Management | `admin.staff` | Admin |
| **Admin** | EventsAir Config | `admin.eventsair` | Admin |
| **Admin** | Field Mappings | `admin.mappings` | Admin |
| **Admin** | Platform Settings | `admin.settings` | Admin |
| **Admin** | Notifications | `admin.notifications` | Admin |
| **Admin** | Access Control | `admin.access_control` | Admin |

*(Note: Admin role will always have an implicit bypass or be explicitly granted all permissions by default).*
# RBAC Fix - Thu May  7 07:38:15 EDT 2026
