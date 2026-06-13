/**
 * TransportDashboard.tsx
 *
 * Transport operator view — vehicle dispatch & assignment.
 * Uses the shared AssignVehicleModal (same as Airport page) for full
 * search, filtering, force-reassignment, and barcode support.
 */

import { useState, useMemo } from 'react'
import { useQueryClient, useQuery, useMutation } from '@tanstack/react-query'
import { Car, RefreshCw, AlertTriangle, CheckCircle, ChevronDown, ChevronUp, Truck } from 'lucide-react'
import toast from 'react-hot-toast'

import { guestsApi, vehiclesApi, dashboardApi } from '../api/services'
import { InboundStatus } from '../types'
import type { GuestSummary } from '../types'
import { AssignVehicleModal } from '../components/fleet/AssignVehicleModal'

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
      (g) => g.inboundStatus === InboundStatus.Arrived && !g.activeVehiclePlate && !g.hasActiveDropOff
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
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center">
              <Car className="w-5 h-5 text-amber-600" />
            </div>
            <div>
              <h1 className="text-xl font-bold text-gray-900">Transportation</h1>
              <p className="text-sm text-gray-500">Vehicle dispatch &amp; assignment</p>
            </div>
          </div>
          <button
            onClick={() => {
              refetch()
              queryClient.invalidateQueries({ queryKey: ['dashboard'] })
            }}
            className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
            title="Refresh"
          >
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>

        {/* Summary stats */}
        {summary && (
          <>
            <div>
              <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                Fleet Overview
              </h2>
              <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                <div className="card text-center">
                  <div className="flex items-center justify-center mb-2">
                    <Car className="w-5 h-5 text-isdb-green" />
                  </div>
                  <p className="text-3xl font-bold text-isdb-green">{summary.vehiclesAvailable ?? 0}</p>
                  <p className="text-sm text-gray-500 mt-1">Vehicles Available</p>
                  <p className="text-xs text-gray-400 mt-0.5">of {(summary as any).vehiclesTotal ?? 0} total</p>
                </div>
                <div className="card text-center">
                  <div className="flex items-center justify-center mb-2">
                    <Car className="w-5 h-5 text-amber-400" />
                  </div>
                  <p className="text-3xl font-bold text-amber-600">{summary.vehiclesAssigned ?? 0}</p>
                  <p className="text-sm text-gray-500 mt-1">Vehicles Assigned</p>
                </div>
                <div className="card text-center">
                  <div className="flex items-center justify-center mb-2">
                    <CheckCircle className="w-5 h-5 text-isdb-green" />
                  </div>
                  <p className="text-3xl font-bold text-isdb-green">{summary.driversAvailable ?? 0}</p>
                  <p className="text-sm text-gray-500 mt-1">Drivers Available</p>
                  <p className="text-xs text-gray-400 mt-0.5">of {(summary as any).driversTotal ?? 0} total</p>
                </div>
                <div className="card text-center border-amber-200 bg-amber-50">
                  <div className="flex items-center justify-center mb-2">
                    <AlertTriangle className="w-5 h-5 text-amber-500" />
                  </div>
                  <p className="text-3xl font-bold text-amber-600">{summary.guestsWithoutVehicle ?? 0}</p>
                  <p className="text-sm text-gray-600 mt-1">Guests Without Vehicle</p>
                </div>
                <div className="card text-center">
                  <div className="flex items-center justify-center mb-2">
                    <Car className="w-5 h-5 text-blue-400" />
                  </div>
                  <p className="text-3xl font-bold text-blue-600">{(summary as any).guestsDeservingVehicle ?? 0}</p>
                  <p className="text-sm text-gray-500 mt-1">Guests Deserving</p>
                  <p className="text-xs text-gray-400 mt-0.5">car class assigned</p>
                </div>
              </div>
            </div>

            {/* Fleet by class */}
            {summary.fleetByClass && summary.fleetByClass.length > 0 && (
              <div>
                <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">
                  Fleet by Class
                </h2>
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
                        className={`card border ${
                          critical
                            ? 'border-red-200 bg-red-50'
                            : warning
                            ? 'border-amber-200 bg-amber-50'
                            : 'border-gray-200'
                        }`}
                      >
                        <div className="flex items-center gap-2 mb-3">
                          <span
                            className="w-3 h-3 rounded-full flex-shrink-0"
                            style={{ backgroundColor: (fc as any).classColor ?? '#6B7280' }}
                          />
                          <span className="font-semibold text-sm text-gray-900 truncate">
                            {fc.className}
                          </span>
                        </div>
                        <div className="grid grid-cols-3 gap-2 text-center">
                          <div>
                            <p className="text-xl font-bold text-isdb-green">{fc.available}</p>
                            <p className="text-xs text-gray-500 mt-0.5">Available</p>
                          </div>
                          <div>
                            <p className="text-xl font-bold text-amber-600">{fc.assigned}</p>
                            <p className="text-xs text-gray-500 mt-0.5">Assigned</p>
                          </div>
                          <div>
                            <p
                              className={`text-xl font-bold ${
                                critical ? 'text-red-600' : warning ? 'text-amber-600' : 'text-gray-700'
                              }`}
                            >
                              {(fc as any).guestsDeserving}
                            </p>
                            <p className="text-xs text-gray-500 mt-0.5">Guests</p>
                          </div>
                        </div>
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
