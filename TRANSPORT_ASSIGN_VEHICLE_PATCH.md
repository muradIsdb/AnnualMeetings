# Transport Assign Vehicle Modal Patch

**Author**: Manus AI
**Date**: May 13, 2026
**Environment**: UAT (Production deployment pending user approval)

## Background

The original IsDB Hospitality Platform frontend source code (React/TypeScript) was not committed to the repository. The only available frontend asset is the compiled, minified bundle file (`src/IsDB.Hospitality.API/wwwroot/assets/index-v4fix.js`).

The **Airport** page had an advanced "Assign Vehicle" modal (with vehicle search, QR scanning, "Show available only" filtering, assignment type selection, and force-reassignment logic). However, the **Transport** dashboard was using an older, simplified modal that lacked these features.

Because the original source code was missing, we performed **direct bundle surgery** on `index-v4fix.js` to extract the advanced modal logic from the Airport page and inject it into the Transport page.

## The Bundle Surgery

The patch was applied directly to the minified JavaScript bundle. Here is a summary of the changes:

### 1. Identifying the Components
- **Airport Advanced Modal (`qv`)**: Located around character position 468,463 in the original bundle. This component rendered both a UI card and the modal overlay.
- **Transport Simple Modal (`d4`)**: Located around character position 538,717.
- **Transport Dashboard (`u4`)**: Located around character position 540,746, which originally called `d4`.

### 2. Creating the New `TAM` Component
We extracted the modal overlay portion of the `qv` function to create a new standalone component called `TAM` (Transport Assign Modal). 

Key modifications made to the extracted code to make it work standalone:
- Removed the internal `[c, d] = N.useState(!1)` state (which originally controlled whether the modal was open), as `TAM` is conditionally rendered by its parent.
- Changed the `useQuery` configuration for fetching vehicles from `enabled: c` to `enabled: !0` (true) so the query executes immediately when the modal mounts.
- Removed the trigger button (`J` function) from the render output, leaving only the modal overlay (`<div className="fixed inset-0...`).
- Added query invalidations for Transport-specific endpoints (`guests/transport-all`, `dashboard/summary/transport`) upon successful assignment.

### 3. Wiring it into the Transport Dashboard
We modified the `u4` (Transport Dashboard) component to:
- Render `TAM` instead of `d4`.
- Pass the correct props (`guestId`, `guestName`, `activeAssignment`, `deservedCarClassId`, `deservedCarClassName`, `deservedCarClassColor`, and an `onClose` callback).

## Reconstructed Source Code

To ensure future maintainability, we have reverse-engineered the patched bundle back into readable React/TypeScript source code. These files are located in the repository but are **not** currently wired into a build pipeline (as the original build pipeline is missing):

- `frontend/src/components/Transport/TransportAssignVehicleModal.tsx`
- `frontend/src/pages/Transport/TransportDashboard.tsx`

If the frontend is ever rebuilt from scratch, these files contain the exact logic currently running in the patched bundle.

## How to Replicate or Modify

If further changes are needed and you must edit the bundle directly:

1. **Locate `TAM`**: Search for `function TAM({guestId:e,guestName:t` in `index-v4fix.js`.
2. **Locate the Dashboard**: Search for `function u4()` to find the Transport Dashboard. The call to `TAM` is located near the end of the `u4` return statement.
3. **Be careful with minified variables**: Variables like `I` (vehicles data), `G` (available vehicles), and `V` (class-filtered vehicles) are highly context-dependent. Refer to `TransportAssignVehicleModal.tsx` to understand the mapping between the minified variables and their original names.

## Deployment Status

This patch has been deployed and verified on the **UAT** environment (`uat` branch). It has not been deployed to production (`master` branch) per user instructions.
