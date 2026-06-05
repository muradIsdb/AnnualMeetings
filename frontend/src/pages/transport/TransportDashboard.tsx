/**
 * TransportDashboard.tsx
 *
 * Transport operator view — vehicle dispatch & assignment.
 * Uses the shared AssignVehicleModal (same as Airport page) for full
 * search, filtering, force-reassignment, and barcode support.
 */

import { useState, useMemo } from 'react'
import { useQueryClient, useQuery, useMutation } from '@tanstack/react-query'
import { Car, RefreshCw, AlertTriangle, CheckCircle, ChevronDown, ChevronUp, Truck, CheckCircle2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { useNavigate } from 'react-router-dom'

import { guestsApi, vehiclesApi, dashboardApi } from '../../api/services'
import { InboundStatus } from '../../types'
import type { GuestSummary } from '../../types'
import { AssignVehicleModal } from '../../components/fleet/AssignVehicleModal'

// ─── Guest Avatar ─────────────────────────────────────────────────────────────

function GuestAvatar({ photoUrl, name }: { photoUrl?: string; name: string }) {
  if (photoUrl) {
    return (
      <img
        src={photoUrl}
        alt={name}
        className="w-9 h-9 rounded-full object-cover flex-shrink-0 border border-gray-200"
      />
    )
  }
  const initials = name
    .split(' ')
    .slice(0, 2)
    .map((n) => n[0])
    .join('')
    .toUpperCase()
  return (
    <div className="w-9 h-9 rounded-full bg-gray-200 flex items-center justify-center flex-shrink-0 text-xs font-bold text-gray-600">
      {initials}
    </div>
  )
}

// ─── Rank Badge ───────────────────────────────────────────────────────────────

function RankBadge({ guest }: { guest: GuestSummary }) {
  if (!guest.rankValue) return null
  const rank = guest.rankValue.toLowerCase()
  const cls =
    rank === 'vvip'
      ? 'bg-purple-100 text-purple-700 border border-purple-300'
      : rank === 'vip'
      ? 'bg-amber-100 text-amber-700 border border-amber-300'
      : 'bg-gray-100 text-gray-700 border border-gray-300'
  return (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-bold ${cls}`}>
      {guest.rankValue}
    </span>
  )
}

// ─── Car Class Badge ──────────────────────────────────────────────────────────

function CarClassBadge({ name, color }: { name?: string; color?: string }) {
  if (!name) return null
  return (
    <span
      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-semibold"
      style={{
        backgroundColor: color ? `${color}20` : '#f3f4f6',
        color: color ?? '#6b7280',
        border: `1px solid ${color ?? '#d1d5db'}`,
      }}
    >
      <span
        className="w-1.5 h-1.5 rounded-full flex-shrink-0"
        style={{ backgroundColor: color ?? '#6b7280' }}
      />
      {name}
    </span>
  )
}

// ─── Awaiting Dispatch Card ───────────────────────────────────────────────────

function AwaitingCard({
  guest,
  onAssign,
}: {
  guest: GuestSummary
  onAssign: (guest: GuestSummary) => void
}) {
  return (
    <div className="rounded-xl border border-red-200 bg-red-50 p-3 flex items-center gap-3">
      <GuestAvatar photoUrl={guest.photoUrl} name={guest.fullName} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="font-semibold text-sm text-gray-900 truncate">{guest.fullName}</span>
          <RankBadge guest={guest} />
          {guest.isCritical && (
            <span className="inline-flex items-center px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-purple-100 text-purple-700">
              Critical
            </span>
          )}
        </div>
        <p className="text-xs text-gray-500 truncate">{guest.designation}</p>
        <div className="flex items-center gap-2 mt-1 flex-wrap">
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-100 text-red-700">
            <AlertTriangle className="w-2.5 h-2.5" />
            Arrived — Needs Vehicle
          </span>
          <CarClassBadge name={guest.deservedCarClassName} color={guest.deservedCarClassColor} />
        </div>
      </div>
      <button
        onClick={() => onAssign(guest)}
        className="flex-shrink-0 flex items-center gap-1.5 px-3 py-1.5 bg-isdb-green text-white text-xs font-semibold rounded-lg hover:bg-isdb-green/90 transition-colors"
      >
        <Car className="w-3.5 h-3.5" />
        Assign
      </button>
    </div>
  )
}

// ─── In-Transit Card ──────────────────────────────────────────────────────────

function InTransitCard({ guest }: { guest: GuestSummary }) {
  const label =
    guest.inboundStatus === InboundStatus.ReceivedByEmbassyTeam
      ? 'Received by Embassy'
      : guest.inboundStatus === InboundStatus.VehicleAssigned
      ? 'Vehicle Assigned'
      : 'On the Way'
  const labelCls =
    guest.inboundStatus === InboundStatus.ReceivedByEmbassyTeam
      ? 'bg-amber-100 text-amber-700'
      : 'bg-blue-100 text-blue-700'

  return (
    <div className="rounded-xl border border-blue-100 bg-blue-50 p-3 flex items-center gap-3">
      <GuestAvatar photoUrl={guest.photoUrl} name={guest.fullName} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5 flex-wrap">
          <span className="font-semibold text-sm text-gray-900 truncate">{guest.fullName}</span>
          <RankBadge guest={guest} />
        </div>
        <p className="text-xs text-gray-500 truncate">{guest.designation}</p>
        <div className="flex items-center gap-2 mt-1 flex-wrap">
          <span className={`inline-flex items-center gap-1 text-[10px] font-medium px-1.5 py-0.5 rounded-full ${labelCls}`}>
            <Truck className="w-2.5 h-2.5" />
            {label}
          </span>
          {guest.activeVehiclePlate && (
            <span className="inline-flex items-center gap-1 text-[10px] text-blue-700 font-medium bg-blue-100 px-1.5 py-0.5 rounded-full">
              <Car className="w-2.5 h-2.5" />
              {guest.activeVehiclePlate}
            </span>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Departures Card ──────────────────────────────────────────────────────────

function DepartureCard({ guest }: { guest: GuestSummary }) {
  const hasVehicle = !!guest.activeVehiclePlate
  return (
    <div className="flex items-center gap-3 py-2.5 border-b border-gray-100 last:border-0">
      <GuestAvatar photoUrl={guest.photoUrl} name={guest.fullName} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="font-medium text-sm text-gray-900 truncate">{guest.fullName}</span>
          <RankBadge guest={guest} />
        </div>
        <p className="text-xs text-gray-500 truncate">{guest.designation}</p>
      </div>
      <div className="flex items-center gap-2 flex-shrink-0">
        {hasVehicle ? (
          <span className="inline-flex items-center gap-1 text-[10px] text-green-700 font-medium bg-green-100 px-1.5 py-0.5 rounded-full">
            <Car className="w-2.5 h-2.5" />
            {guest.activeVehiclePlate}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-[10px] text-amber-700 font-medium bg-amber-100 px-1.5 py-0.5 rounded-full">
            <AlertTriangle className="w-2.5 h-2.5" />
            No vehicle
          </span>
        )}
        <span
          className={`text-[10px] font-medium px-1.5 py-0.5 rounded-full ${
            guest.statusLabel === 'AtAirportDeparture'
              ? 'bg-purple-100 text-purple-700'
              : 'bg-gray-100 text-gray-600'
          }`}
        >
          {guest.statusLabel === 'AtAirportDeparture' ? 'At Airport' : 'Departing'}
        </span>
      </div>
    </div>
  )
}

// ─── Main Dashboard ───────────────────────────────────────────────────────────

export default function TransportDashboard() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [selectedGuest, setSelectedGuest] = useState<GuestSummary | null>(null)
  const [searchQuery, setSearchQuery] = useState('')
  const [departuresOpen, setDeparturesOpen] = useState(true)

  // ── Data fetching ──────────────────────────────────────────────────────────
  const { data: guests = [], isLoading, refetch } = useQuery({
    queryKey: ['guests', 'transport-all'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30_000,
  })

  const { data: summary } = useQuery({
    queryKey: ['dashboard', 'summary', 'transport'],
    queryFn: dashboardApi.getSummary,
    refetchInterval: 30_000,
  })

  // ── Derived lists ──────────────────────────────────────────────────────────
  const awaitingDispatch = useMemo(() => {
    const list = guests.filter(
      (g) => g.inboundStatus === InboundStatus.Arrived && !g.activeVehiclePlate
    )
    const priority = (g: GuestSummary) => {
      const r = g.rankValue?.toLowerCase()
      return r === 'vvip' ? 0 : r === 'vip' ? 1 : g.isCritical ? 2 : 3
    }
    return list.sort((a, b) => priority(a) - priority(b))
  }, [guests])

  const filteredAwaiting = useMemo(() => {
    if (!searchQuery) return awaitingDispatch
    const q = searchQuery.toLowerCase()
    return awaitingDispatch.filter(
      (g) =>
        g.fullName.toLowerCase().includes(q) ||
        (g.designation ?? '').toLowerCase().includes(q)
    )
  }, [awaitingDispatch, searchQuery])

  const inTransit = useMemo(
    () =>
      guests.filter(
        (g) =>
          g.inboundStatus === InboundStatus.VehicleAssigned ||
          g.inboundStatus === InboundStatus.ReceivedByEmbassyTeam
      ),
    [guests]
  )

  const departures = useMemo(() => {
    const priority = (g: GuestSummary) => {
      const r = g.rankValue?.toLowerCase()
      return r === 'vvip' ? 0 : r === 'vip' ? 1 : 2
    }
    return guests
      .filter(
        (g) =>
          g.statusLabel === 'DepartingHotel' || g.statusLabel === 'AtAirportDeparture'
      )
      .sort((a, b) => priority(a) - priority(b))
  }, [guests])

  // ── Assign vehicle mutation (called by AssignVehicleModal on success) ──────
  // The modal handles assign + force-assign internally; we just need to
  // invalidate queries and close the modal on success.
  const handleAssignSuccess = () => {
    queryClient.invalidateQueries({ queryKey: ['guests'] })
    queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    setSelectedGuest(null)
  }

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col h-full overflow-auto">
      <div className="p-4 md:p-6 space-y-5 max-w-7xl mx-auto w-full">

        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center">
              <Car className="w-5 h-5 text-amber-600" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Transport Dashboard</h1>
              <p className="text-xs sm:text-sm text-gray-400 mt-0.5">Real-time fleet and dispatch overview</p>
            </div>
          </div>
          <div className="flex items-center gap-2 sm:gap-3">
            <span className="text-xs text-gray-400 hidden sm:inline">Auto-refreshes every 30s</span>
            <button
              onClick={() => {
                refetch()
                queryClient.invalidateQueries({ queryKey: ['dashboard'] })
              }}
              className="flex items-center gap-1.5 text-sm text-gray-600 border border-gray-200 rounded-lg px-3 py-1.5 bg-white hover:bg-gray-50 w-full sm:w-auto justify-center transition-colors"
              title="Refresh"
            >
              <RefreshCw className="w-4 h-4" /> Refresh
            </button>
          </div>
        </div>

        {/* Summary stats */}
        {summary && (
          <>
            <div>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">
                Fleet Status
              </p>
              <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-2 sm:gap-3">
                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow">
                  <p className="text-xs text-gray-400 mb-1">Total Vehicles</p>
                  <p className="text-2xl sm:text-3xl font-bold text-gray-900">{(summary as any).vehiclesTotal ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">entire fleet</p>
                </div>

                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow">
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className="w-2 h-2 rounded-full bg-green-500 inline-block flex-shrink-0"></span>
                    <p className="text-xs text-gray-400">Available</p>
                  </div>
                  <p className="text-2xl sm:text-3xl font-bold text-green-600">{summary.vehiclesAvailable ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">ready to assign</p>
                </div>

                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow">
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className="w-2 h-2 rounded-full bg-amber-400 inline-block flex-shrink-0"></span>
                    <p className="text-xs text-gray-400">Assigned</p>
                  </div>
                  <p className="text-2xl sm:text-3xl font-bold text-amber-500">{summary.vehiclesAssigned ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">with a guest</p>
                </div>

                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow">
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className="w-2 h-2 rounded-full bg-gray-400 inline-block flex-shrink-0"></span>
                    <p className="text-xs text-gray-400">Not Provided</p>
                  </div>
                  <p className="text-2xl sm:text-3xl font-bold text-gray-500">{(summary as any).vehiclesNotProvided ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">pending delivery</p>
                </div>

                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow">
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className="w-2 h-2 rounded-full bg-red-400 inline-block flex-shrink-0"></span>
                    <p className="text-xs text-gray-400">Out of Service</p>
                  </div>
                  <p className="text-2xl sm:text-3xl font-bold text-red-500">{(summary as any).vehiclesOutOfService ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">unavailable</p>
                </div>

                <div className="bg-white rounded-2xl border border-gray-100 p-3 sm:p-4 hover:shadow-md transition-shadow col-span-2 sm:col-span-1">
                  <div className="flex items-center gap-1.5 mb-1">
                    <span className="w-2 h-2 rounded-full bg-blue-500 inline-block flex-shrink-0"></span>
                    <p className="text-xs text-gray-400">Drivers</p>
                  </div>
                  <p className="text-2xl sm:text-3xl font-bold text-blue-600">{summary.driversAvailable ?? 0}</p>
                  <p className="text-xs text-gray-400 mt-1">of {(summary as any).driversTotal ?? 0} total</p>
                </div>
              </div>
            </div>

            {/* GUESTS NEEDING ATTENTION */}
            <div>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">Guests Needing Attention</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div className="rounded-2xl border-2 border-amber-200 bg-amber-50 p-4 flex items-center justify-between gap-3">
                  <div className="flex items-start gap-3 min-w-0">
                    <AlertTriangle className="text-amber-500 w-6 h-6 mt-0.5 flex-shrink-0" />
                    <div className="min-w-0">
                      <p className="text-2xl font-bold text-amber-700">{summary.guestsWithoutVehicle ?? 0}</p>
                      <p className="text-sm font-semibold text-amber-800 leading-tight">Guests Without Vehicle</p>
                      <p className="text-xs text-amber-600 mt-0.5">Deserve a car class · need assignment</p>
                    </div>
                  </div>
                  <button 
                    onClick={() => {
                      const searchInput = document.querySelector('input[placeholder*="Search"]') as HTMLInputElement;
                      if (searchInput) searchInput.focus();
                    }}
                    className="bg-amber-500 hover:bg-amber-600 text-white text-xs sm:text-sm font-semibold px-3 sm:px-4 py-2 rounded-xl flex items-center gap-1 transition-colors flex-shrink-0"
                  >
                    <Truck className="w-4 h-4" /> <span className="hidden sm:inline">Dispatch</span><span className="sm:hidden">Go</span>
                  </button>
                </div>

                <div className="rounded-2xl border border-green-200 bg-green-50 p-4 flex items-center justify-between gap-3">
                  <div className="flex items-start gap-3 min-w-0">
                    <CheckCircle2 className="text-green-500 w-6 h-6 mt-0.5 flex-shrink-0" />
                    <div className="min-w-0">
                      <p className="text-2xl font-bold text-green-700">{(summary as any).guestsAssignedWithoutDedicated ?? 0}</p>
                      <p className="text-sm font-semibold text-green-800 leading-tight">Assigned w/o Dedicated Car</p>
                      <p className="text-xs text-green-600 mt-0.5">No anomalies detected</p>
                    </div>
                  </div>
                  <span className="text-xs font-semibold bg-green-100 text-green-700 px-3 py-1 rounded-full border border-green-200 flex-shrink-0">All Clear</span>
                </div>
              </div>
            </div>

            {/* Fleet by class */}
            {summary.fleetByClass && summary.fleetByClass.length > 0 && (
              <div>
                <div className="flex items-center justify-between mb-3">
                  <div>
                    <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Fleet by Class</p>
                    <p className="text-xs text-gray-400 mt-0.5">Vehicle availability per car class</p>
                  </div>
                  <div className="flex gap-2">
                    <button 
                      onClick={() => navigate('/transport/fleet')}
                      className="text-xs border border-gray-200 text-gray-600 px-3 py-1.5 rounded-lg font-medium hover:bg-gray-50 transition-colors"
                    >
                      Fleet Mgmt
                    </button>
                  </div>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {summary.fleetByClass.map((fc) => {
                    const critical = (fc as any).guestsDeserving > 0 && fc.available === 0
                    const warning =
                      (fc as any).guestsDeserving > fc.available &&
                      fc.available >= 0 &&
                      (fc as any).guestsDeserving > 0
                    return (
                      <div
                        key={(fc as any).classId ?? fc.className}
                        className={`bg-white rounded-2xl border p-4 hover:shadow-md transition-shadow ${
                          critical
                            ? 'border-red-200 bg-red-50/30'
                            : warning
                            ? 'border-amber-200 bg-amber-50/30'
                            : 'border-gray-100'
                        }`}
                      >
                        <div className="flex items-center justify-between mb-3">
                          <div className="flex items-center gap-2 min-w-0">
                            <span
                              className="w-3 h-3 rounded-full flex-shrink-0"
                              style={{ backgroundColor: (fc as any).classColor ?? '#6B7280' }}
                            />
                            <span className="text-sm font-semibold text-gray-800 truncate" title={fc.className}>
                              {fc.className}
                            </span>
                          </div>
                          <span className="text-xs font-bold text-gray-500 bg-gray-50 px-2 py-0.5 rounded-full flex-shrink-0 border border-gray-100">
                            {(fc as any).totalVehicles} total
                          </span>
                        </div>
                        
                        <div className="grid grid-cols-4 gap-1 text-center">
                          <div className="bg-green-50 rounded-xl py-2">
                            <p className="text-lg font-bold text-green-600">{fc.available}</p>
                            <p className="text-[10px] sm:text-xs text-green-600 mt-0.5">Avail.</p>
                          </div>
                          <div className="bg-amber-50 rounded-xl py-2">
                            <p className="text-lg font-bold text-amber-500">{fc.assigned}</p>
                            <p className="text-[10px] sm:text-xs text-amber-500 mt-0.5">Assigned</p>
                          </div>
                          <div className="bg-gray-50 rounded-xl py-2">
                            <p className="text-lg font-bold text-gray-400">{(fc as any).notProvided ?? 0}</p>
                            <p className="text-[10px] sm:text-xs text-gray-400 mt-0.5 truncate px-1">Not Prov.</p>
                          </div>
                          <div className={`rounded-xl py-2 ${(fc as any).guestsDeserving > 0 ? 'bg-blue-50' : 'bg-gray-50'}`}>
                            <p className={`text-lg font-bold ${(fc as any).guestsDeserving > 0 ? 'text-blue-600' : 'text-gray-400'}`}>
                              {(fc as any).guestsDeserving}
                            </p>
                            <p className={`text-[10px] sm:text-xs mt-0.5 ${(fc as any).guestsDeserving > 0 ? 'text-blue-500' : 'text-gray-400'}`}>
                              Guests
                            </p>
                          </div>
                        </div>
                        
                        {(fc as any).outOfService > 0 && (
                          <div className="mt-2 text-xs text-red-500 font-medium text-center bg-red-50 py-1 rounded-lg">
                            {(fc as any).outOfService} out of service
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              </div>
            )}
          </>
        )}

        {/* Main grid: Awaiting Dispatch + In Transit */}
        <div className="grid grid-cols-1 lg:grid-cols-5 gap-5">
          {/* Awaiting Dispatch — 3 cols */}
          <div className="lg:col-span-3 space-y-3">
            <div className="flex items-center justify-between">
              <h2 className="font-semibold text-gray-900 flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 text-red-500" />
                Awaiting Dispatch
                <span className="text-sm font-normal text-gray-400">({awaitingDispatch.length})</span>
              </h2>
            </div>

            {/* Search */}
            {awaitingDispatch.length > 0 && (
              <div className="relative">
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search by name or designation…"
                  className="w-full pl-4 pr-4 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-isdb-green/30"
                />
              </div>
            )}

            {isLoading ? (
              <div className="space-y-2">
                {[...Array(3)].map((_, i) => (
                  <div key={i} className="rounded-xl border border-gray-200 p-3 animate-pulse h-16 bg-gray-100" />
                ))}
              </div>
            ) : filteredAwaiting.length === 0 ? (
              <div className="rounded-xl border border-green-200 bg-green-50 p-8 text-center">
                <CheckCircle className="w-10 h-10 text-green-400 mx-auto mb-2" />
                <p className="text-sm font-medium text-green-700">All arrived guests have vehicles assigned.</p>
                <p className="text-xs text-gray-500 mt-1">No guests awaiting dispatch.</p>
              </div>
            ) : (
              <div className="space-y-2">
                {filteredAwaiting.map((g) => (
                  <AwaitingCard key={g.id} guest={g} onAssign={setSelectedGuest} />
                ))}
              </div>
            )}
          </div>

          {/* In Transit — 2 cols */}
          <div className="lg:col-span-2 space-y-3">
            <h2 className="font-semibold text-gray-900 flex items-center gap-2">
              <Truck className="w-4 h-4 text-blue-500" />
              In Transit → Hotel
              <span className="text-sm font-normal text-gray-400">({inTransit.length})</span>
            </h2>
            {inTransit.length === 0 ? (
              <div className="rounded-xl border border-gray-200 bg-gray-50 p-6 text-center">
                <Truck className="w-8 h-8 text-gray-300 mx-auto mb-2" />
                <p className="text-sm text-gray-400">No active journeys.</p>
              </div>
            ) : (
              <div className="space-y-2">
                {inTransit.map((g) => (
                  <InTransitCard key={g.id} guest={g} />
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Departures Pipeline */}
        {departures.length > 0 && (
          <div className="rounded-xl border border-gray-200 bg-white overflow-hidden">
            <button
              onClick={() => setDeparturesOpen(!departuresOpen)}
              className="w-full flex items-center justify-between px-4 py-3 hover:bg-gray-50 transition-colors"
            >
              <h2 className="font-semibold text-gray-900 flex items-center gap-2">
                <Car className="w-4 h-4 text-purple-500" />
                Departures Pipeline
                <span className="text-sm font-normal text-gray-400">({departures.length} guests)</span>
              </h2>
              {departuresOpen ? (
                <ChevronUp className="w-4 h-4 text-gray-400" />
              ) : (
                <ChevronDown className="w-4 h-4 text-gray-400" />
              )}
            </button>
            {departuresOpen && (
              <div className="px-4 pb-2">
                {departures.map((g) => (
                  <DepartureCard key={g.id} guest={g} />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Assign Vehicle Modal — shared with Airport page */}
      {selectedGuest && (
        <AssignVehicleModal
          guest={{
            id: selectedGuest.id,
            fullName: selectedGuest.fullName,
            deservedCarClassName: selectedGuest.deservedCarClassName,
            deservedCarClassColor: selectedGuest.deservedCarClassColor,
            deservedCarClassId: selectedGuest.deservedCarClassId,
            activeVehiclePlate: selectedGuest.activeVehiclePlate,
            activeVehicleId: selectedGuest.activeVehicleId,
          }}
          onClose={() => setSelectedGuest(null)}
          onSuccess={handleAssignSuccess}
        />
      )}
    </div>
  )
}
