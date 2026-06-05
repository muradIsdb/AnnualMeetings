/**
 * TransportRoleDashboard.tsx
 *
 * Transport Dashboard page — shown under Transport > Transport Dashboard.
 *
 * Sections:
 *  1. Fleet Status Summary  — 6 KPI cards (Total, Available, Assigned,
 *                             Not Provided, Out of Service, Drivers Available)
 *  2. Guests Needing Attention — Without Vehicle card + Anomalies card
 *  3. Fleet by Class        — one card per class with progress bars
 *                             (hidden when no classes configured)
 *  4. Guests Currently In Transit — clickable table → guest detail
 *                             (hidden when empty)
 *  5. Recent Activity       — last 10 events (hidden when no data)
 *  6. Quick Actions         — Dispatch Queue + Fleet Management buttons
 *
 * Data sources:
 *  - dashboardApi.getSummary()      → DashboardSummaryDto
 *  - vehiclesApi.getAllWithStatus()  → VehicleWithStatusDto[]
 *
 * Auto-refreshes every 60 seconds.
 *
 * NOTE: This file is the readable source reference for the minified bundle
 * (index-carclass-hist-v15.js). It is not yet wired into a build pipeline.
 * When making changes, update this file first, then patch the bundle.
 */

import { useNavigate } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  RefreshCw,
  Loader2,
  Users,
  Truck,
  Car,
  AlertTriangle,
  CheckCircle,
} from 'lucide-react'
import { dashboardApi } from '../../api/services'
import { vehiclesApi } from '../../api/services'

// ─────────────────────────────────────────────────────────────────────────────
// Types (mirrors DashboardSummaryDto from the API)
// ─────────────────────────────────────────────────────────────────────────────

interface FleetByClassItem {
  className: string
  classColor?: string
  available: number
  assigned: number
  notProvided?: number
  outOfService?: number
  guestsDeserving: number
  totalVehicles?: number
}

interface RecentActivityItem {
  type: 'VehicleAssigned' | 'VehicleUnassigned' | 'StatusChanged'
  occurredAt: string
  vehiclePlate?: string
  guestName?: string
  detail?: string
}

interface GuestInTransit {
  id: number
  fullName: string
  nationality?: string
  designation?: string
  deservedCarClassName?: string
  deservedCarClassColor?: string
  activeVehiclePlate?: string
}

interface GuestStatusGroup {
  statusLabel: string
  guests: GuestInTransit[]
}

interface TrdDashboardSummary {
  vehiclesAvailable: number
  vehiclesAssigned: number
  vehiclesNotProvided: number
  vehiclesOutOfService: number
  driversAvailable: number
  driversTotal: number
  guestsWithoutVehicle: number
  fleetByClass?: FleetByClassItem[]
  guestsByStatus?: GuestStatusGroup[]
  recentActivity?: RecentActivityItem[]
}

interface TrdVehicleWithStatus {
  currentGuestId?: number
  carClassId?: number
  carNumber?: string
  driverName?: string
  currentAssignmentType?: 'Dedicated' | 'Pool'
}

// ─────────────────────────────────────────────────────────────────────────────
// Sub-component: TRD_FleetClassCard
// ─────────────────────────────────────────────────────────────────────────────

interface FleetClassCardProps {
  className: string
  color?: string
  available: number
  assigned: number
  notProvided?: number
  outOfService?: number
  guests: number
  total: number
}

function TRD_FleetClassCard({
  className,
  color,
  available,
  assigned,
  notProvided = 0,
  outOfService = 0,
  guests,
  total,
}: FleetClassCardProps) {
  const max = Math.max(total, 1)

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4">
      {/* Header row */}
      <div className="flex items-center gap-2 mb-3">
        <span
          className="w-2.5 h-2.5 rounded-full flex-shrink-0"
          style={{ backgroundColor: color ?? '#6b7280' }}
        />
        <span className="text-sm font-semibold text-gray-800 truncate flex-1">{className}</span>
        <span className="text-xs text-gray-400 font-medium">{total} total</span>
      </div>

      {/* Progress bars */}
      <div className="flex flex-col gap-2">
        {/* Available */}
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500 w-24 flex-shrink-0">Available</span>
          <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 rounded-full"
              style={{ width: `${(available / max) * 100}%` }}
            />
          </div>
          <span className="text-xs font-semibold text-green-600 w-5 text-right">{available}</span>
        </div>

        {/* Assigned */}
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500 w-24 flex-shrink-0">Assigned</span>
          <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 rounded-full"
              style={{ width: `${(assigned / max) * 100}%` }}
            />
          </div>
          <span className="text-xs font-semibold text-blue-600 w-5 text-right">{assigned}</span>
        </div>

        {/* Not Provided */}
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500 w-24 flex-shrink-0">Not Provided</span>
          <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-slate-400 rounded-full"
              style={{ width: `${(notProvided / max) * 100}%` }}
            />
          </div>
          <span className="text-xs font-semibold text-slate-500 w-5 text-right">{notProvided}</span>
        </div>

        {/* Out of Service */}
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500 w-24 flex-shrink-0">Out of Service</span>
          <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-full bg-red-400 rounded-full"
              style={{ width: `${(outOfService / max) * 100}%` }}
            />
          </div>
          <span className="text-xs font-semibold text-red-500 w-5 text-right">{outOfService}</span>
        </div>

        {/* Guests deserving this class */}
        <div className="mt-1 pt-2 border-t border-gray-100">
          <span className="text-xs text-gray-500">
            <Users className="w-3 h-3 inline mr-0.5 text-gray-400" />
            <strong className="text-gray-700">{guests}</strong> guests deserving this class
          </span>
        </div>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// Main component: TransportRoleDashboard
// ─────────────────────────────────────────────────────────────────────────────

export default function TransportRoleDashboard() {
  const navigate = useNavigate()

  // ── Data fetching ──────────────────────────────────────────────────────────
  const {
    data: summary,
    isLoading,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ['dashboard', 'summary', 'trd'],
    queryFn: () => dashboardApi.getSummary() as unknown as Promise<TrdDashboardSummary>,
    refetchInterval: 60_000, // auto-refresh every 60 seconds
  })

  const { data: allVehicles = [] } = useQuery({
    queryKey: ['vehicles', 'all-with-status', 'trd'],
    queryFn: () => vehiclesApi.getAllWithStatus() as unknown as Promise<TrdVehicleWithStatus[]>,
    refetchInterval: 60_000,
  })

  // ── Derived values ─────────────────────────────────────────────────────────

  /** Vehicles assigned to a guest but without a dedicated car class */
  const anomalies = allVehicles.filter((v) => v.currentGuestId && !v.carClassId).length

  /** Total vehicles across all statuses */
  const totalVehicles =
    (summary?.vehiclesAvailable ?? 0) +
    (summary?.vehiclesAssigned ?? 0) +
    (summary?.vehiclesNotProvided ?? 0) +
    (summary?.vehiclesOutOfService ?? 0)

  /** Guests currently in transit (status = VehicleAssigned) */
  const inTransitGuests =
    summary?.guestsByStatus?.find((g) => g.statusLabel === 'VehicleAssigned')?.guests ?? []

  /** Map guestId → vehicle for quick lookup in the in-transit table */
  const vehicleByGuestId = Object.fromEntries(
    allVehicles.map((v) => [v.currentGuestId, v])
  )

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="p-4 md:p-6 max-w-5xl mx-auto">

      {/* ── Page header ── */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Transport Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Real-time fleet and dispatch overview</p>
        </div>
        <div className="text-right">
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
          <p className="text-xs text-gray-400 mt-1">Auto-refreshes every 60s</p>
        </div>
      </div>

      {/* ── Loading state ── */}
      {isLoading ? (
        <div className="flex items-center justify-center h-48 text-gray-400">
          <Loader2 className="w-8 h-8 animate-spin" />
        </div>
      ) : (
        <>

          {/* ── SECTION 1: Fleet Status Summary ── */}
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            Fleet Status Summary
          </p>
          <div className="grid grid-cols-3 md:grid-cols-6 gap-3 mb-5">
            {/* Total */}
            <div className="rounded-xl border border-gray-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-gray-700">{totalVehicles}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Total Vehicles</p>
              <p className="text-xs text-gray-400">entire fleet</p>
            </div>
            {/* Available */}
            <div className="rounded-xl border border-green-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-green-600">{summary?.vehiclesAvailable ?? 0}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Available</p>
              <p className="text-xs text-gray-400">ready to be assigned</p>
            </div>
            {/* Assigned */}
            <div className="rounded-xl border border-blue-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-blue-600">{summary?.vehiclesAssigned ?? 0}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Assigned</p>
              <p className="text-xs text-gray-400">with a guest</p>
            </div>
            {/* Not Provided */}
            <div className="rounded-xl border border-slate-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-slate-500">{summary?.vehiclesNotProvided ?? 0}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Not Provided</p>
              <p className="text-xs text-gray-400">pending delivery</p>
            </div>
            {/* Out of Service */}
            <div className="rounded-xl border border-red-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-red-500">{summary?.vehiclesOutOfService ?? 0}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Out of Service</p>
              <p className="text-xs text-gray-400">unavailable</p>
            </div>
            {/* Drivers Available */}
            <div className="rounded-xl border border-indigo-200 bg-white p-4 text-center">
              <p className="text-2xl font-bold text-indigo-600">{summary?.driversAvailable ?? 0}</p>
              <p className="text-xs font-medium text-gray-500 mt-1">Drivers Available</p>
              <p className="text-xs text-gray-400">of {summary?.driversTotal ?? 0} total</p>
            </div>
          </div>

          {/* ── SECTION 2: Guests Needing Attention ── */}
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            Guests Needing Attention
          </p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 mb-5">
            {/* Without Vehicle */}
            <div
              onClick={() => {
                if ((summary?.guestsWithoutVehicle ?? 0) > 0) navigate('/transport')
              }}
              className={`rounded-xl border p-4 flex items-center justify-between cursor-pointer transition-colors ${
                (summary?.guestsWithoutVehicle ?? 0) > 0
                  ? 'border-amber-300 bg-amber-50 hover:bg-amber-100'
                  : 'border-gray-200 bg-white'
              }`}
            >
              <div className="flex items-center gap-3">
                <div
                  className={`w-11 h-11 rounded-lg flex items-center justify-center ${
                    (summary?.guestsWithoutVehicle ?? 0) > 0 ? 'bg-amber-100' : 'bg-gray-100'
                  }`}
                >
                  <AlertTriangle
                    className={`w-5 h-5 ${
                      (summary?.guestsWithoutVehicle ?? 0) > 0 ? 'text-amber-500' : 'text-gray-400'
                    }`}
                  />
                </div>
                <div>
                  <p
                    className={`text-2xl font-bold ${
                      (summary?.guestsWithoutVehicle ?? 0) > 0 ? 'text-amber-600' : 'text-gray-700'
                    }`}
                  >
                    {summary?.guestsWithoutVehicle ?? 0}
                  </p>
                  <p className="text-sm font-semibold text-gray-700">Guests Without Vehicle</p>
                  <p className="text-xs text-gray-500">deserve a car class · need assignment</p>
                </div>
              </div>
              {(summary?.guestsWithoutVehicle ?? 0) > 0 && (
                <button className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-500 text-white text-xs font-semibold rounded-lg hover:bg-amber-600 transition-colors">
                  <Car className="w-3.5 h-3.5" />
                  Dispatch
                </button>
              )}
            </div>

            {/* Anomalies */}
            <div
              className={`rounded-xl border p-4 flex items-center justify-between ${
                anomalies > 0 ? 'border-red-300 bg-red-50' : 'border-gray-200 bg-white'
              }`}
            >
              <div className="flex items-center gap-3">
                <div
                  className={`w-11 h-11 rounded-lg flex items-center justify-center ${
                    anomalies > 0 ? 'bg-red-100' : 'bg-green-50'
                  }`}
                >
                  {anomalies > 0 ? (
                    <AlertTriangle className="w-5 h-5 text-red-500" />
                  ) : (
                    <CheckCircle className="w-5 h-5 text-green-500" />
                  )}
                </div>
                <div>
                  <p className={`text-2xl font-bold ${anomalies > 0 ? 'text-red-600' : 'text-green-600'}`}>
                    {anomalies}
                  </p>
                  <p className="text-sm font-semibold text-gray-700">Assigned w/o Dedicated Car</p>
                  <p className="text-xs text-gray-500">
                    {anomalies === 0 ? 'no anomalies detected' : 'needs review'}
                  </p>
                </div>
              </div>
              <span
                className={`text-xs font-semibold px-2.5 py-1 rounded-full ${
                  anomalies > 0 ? 'bg-red-100 text-red-600' : 'bg-green-100 text-green-600'
                }`}
              >
                {anomalies > 0 ? 'Review' : 'All Clear'}
              </span>
            </div>
          </div>

          {/* ── SECTION 3: Fleet by Class (hidden when no classes configured) ── */}
          {summary?.fleetByClass && summary.fleetByClass.length > 0 && (
            <>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                Fleet by Class
              </p>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mb-5">
                {summary.fleetByClass.map((fc) => (
                  <TRD_FleetClassCard
                    key={fc.className}
                    className={fc.className}
                    color={fc.classColor}
                    available={fc.available}
                    assigned={fc.assigned}
                    notProvided={fc.notProvided}
                    outOfService={fc.outOfService ?? 0}
                    guests={fc.guestsDeserving}
                    total={
                      fc.totalVehicles ??
                      fc.available + fc.assigned + (fc.notProvided ?? 0) + (fc.outOfService ?? 0)
                    }
                  />
                ))}
              </div>
            </>
          )}

          {/* ── SECTION 4: Guests Currently In Transit (hidden when empty) ── */}
          {inTransitGuests.length > 0 && (
            <>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                Guests Currently In Transit
              </p>
              <div className="rounded-xl border border-gray-200 bg-white overflow-hidden mb-5">
                <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
                  <h3 className="text-sm font-semibold text-gray-900">Guests Currently In Transit</h3>
                  <span className="text-xs text-gray-400">VehicleAssigned · on the way to hotel</span>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="bg-gray-50 border-b border-gray-100">
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Guest
                        </th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Car Class
                        </th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Vehicle
                        </th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Driver
                        </th>
                        <th className="text-left px-4 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Type
                        </th>
                        <th className="w-6" />
                      </tr>
                    </thead>
                    <tbody>
                      {inTransitGuests.map((guest) => {
                        const vehicle = vehicleByGuestId[guest.id]
                        return (
                          <tr
                            key={guest.id}
                            onClick={() => navigate(`/airport/guest/${guest.id}`)}
                            className="border-b border-gray-50 last:border-0 hover:bg-gray-50 cursor-pointer transition-colors"
                          >
                            {/* Guest name + subtitle */}
                            <td className="px-4 py-3">
                              <p className="font-semibold text-gray-900 text-sm">{guest.fullName}</p>
                              <p className="text-xs text-gray-400">
                                {[guest.nationality, guest.designation].filter(Boolean).join(' · ')}
                              </p>
                            </td>
                            {/* Car Class badge */}
                            <td className="px-4 py-3">
                              {guest.deservedCarClassName ? (
                                <span
                                  className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold text-white"
                                  style={{ backgroundColor: guest.deservedCarClassColor ?? '#6b7280' }}
                                >
                                  {guest.deservedCarClassName}
                                </span>
                              ) : (
                                <span className="text-xs text-gray-400">—</span>
                              )}
                            </td>
                            {/* Plate + Car Number sticker */}
                            <td className="px-4 py-3">
                              <div className="flex items-center gap-1.5">
                                <span className="font-mono text-xs text-gray-700">
                                  {guest.activeVehiclePlate ?? '—'}
                                </span>
                                {vehicle?.carNumber && (
                                  <span className="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-bold bg-orange-50 text-orange-700 border border-orange-200 font-mono">
                                    {vehicle.carNumber}
                                  </span>
                                )}
                              </div>
                            </td>
                            {/* Driver */}
                            <td className="px-4 py-3 text-sm text-gray-600">
                              {vehicle?.driverName ?? '—'}
                            </td>
                            {/* Assignment Type */}
                            <td className="px-4 py-3">
                              {vehicle?.currentAssignmentType ? (
                                <span
                                  className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                                    vehicle.currentAssignmentType === 'Dedicated'
                                      ? 'bg-blue-50 text-blue-700'
                                      : 'bg-green-50 text-green-700'
                                  }`}
                                >
                                  {vehicle.currentAssignmentType}
                                </span>
                              ) : (
                                <span className="text-xs text-gray-400">—</span>
                              )}
                            </td>
                            {/* Chevron */}
                            <td className="px-4 py-3 text-gray-300 text-base">›</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            </>
          )}

          {/* ── SECTION 5: Recent Activity (hidden when no data) ── */}
          {summary?.recentActivity && summary.recentActivity.length > 0 && (
            <>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                Recent Activity
              </p>
              <div className="rounded-xl border border-gray-200 bg-white overflow-hidden mb-5">
                <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
                  <h3 className="text-sm font-semibold text-gray-900">Recent Activity</h3>
                  <span className="text-xs text-gray-400">Last 10 events</span>
                </div>
                <div>
                  {summary.recentActivity.slice(0, 10).map((activity, idx) => {
                    const dotColor =
                      activity.type === 'VehicleAssigned'
                        ? 'bg-green-500'
                        : activity.type === 'VehicleUnassigned'
                        ? 'bg-gray-400'
                        : 'bg-amber-400'

                    const time = new Date(activity.occurredAt).toLocaleTimeString('en', {
                      hour: '2-digit',
                      minute: '2-digit',
                    })

                    const text =
                      (activity.type === 'VehicleAssigned'
                        ? `Vehicle ${activity.vehiclePlate ? activity.vehiclePlate + ' ' : ''}assigned to `
                        : activity.type === 'VehicleUnassigned'
                        ? `Vehicle ${activity.vehiclePlate ? activity.vehiclePlate + ' ' : ''}unassigned from `
                        : 'Status change: ') +
                      (activity.guestName ?? '') +
                      (activity.detail ? ` · ${activity.detail}` : '')

                    return (
                      <div
                        key={idx}
                        className="flex items-start gap-3 px-4 py-3 border-b border-gray-50 last:border-0"
                      >
                        <span className={`w-2 h-2 rounded-full mt-1.5 flex-shrink-0 ${dotColor}`} />
                        <span className="text-xs text-gray-400 whitespace-nowrap mt-0.5 w-10 flex-shrink-0">
                          {time}
                        </span>
                        <span className="text-sm text-gray-600 leading-snug">{text}</span>
                      </div>
                    )
                  })}
                </div>
              </div>
            </>
          )}

          {/* ── SECTION 6: Quick Actions ── */}
          <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
            Quick Actions
          </p>
          <div className="flex flex-wrap gap-3">
            <button
              onClick={() => navigate('/transport')}
              className="flex items-center gap-2 px-4 py-2 bg-isdb-green text-white text-sm font-semibold rounded-lg hover:bg-isdb-green/90 transition-colors"
            >
              <Car className="w-4 h-4" />
              Dispatch Queue
            </button>
            <button
              onClick={() => navigate('/fleet')}
              className="flex items-center gap-2 px-4 py-2 border border-gray-200 text-gray-700 text-sm font-semibold rounded-lg hover:bg-gray-50 transition-colors"
            >
              <Truck className="w-4 h-4" />
              Fleet Management
            </button>
          </div>

        </>
      )}
    </div>
  )
}
