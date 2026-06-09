/**
 * AssignVehicleModal — shared advanced vehicle assignment modal.
 *
 * Used by both the Airport (GuestDetailPage) and Transport (TransportDashboard)
 * pages. Provides:
 *   • Full-text search (plate, car #, make, model, driver)
 *   • "Show available only" / "Show all vehicles" toggle
 *   • Car-class colour indicators and "Show [class] only" filter
 *   • Force-reassignment flow with displaced-guest warning
 *   • QR / barcode scan shortcut
 *   • Assignment-type selector (Dedicated / Drop-off)
 *   • Estimated arrival time input
 */

import { useRef, useState, useEffect } from 'react'
import { useQueryClient, useQuery, useMutation } from '@tanstack/react-query'
import { X, User, QrCode, CheckCircle, AlertTriangle, Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { vehiclesApi } from '../../api/services'
import type { VehicleWithStatus } from '../../types'

// ─── Types ────────────────────────────────────────────────────────────────────

export interface AssignVehicleGuest {
  id: string
  fullName: string
  deservedCarClassName?: string
  deservedCarClassColor?: string
  deservedCarClassId?: string
  /** If set, the guest already has a vehicle assigned (shows 'Change Vehicle' instead of 'Assign Vehicle') */
  activeVehiclePlate?: string
  activeVehicleId?: string
}

// Use VehicleWithStatus as the vehicle option type (matches all-with-status API response)
type VehicleOption = VehicleWithStatus

interface AssignVehicleModalProps {
  guest: AssignVehicleGuest
  onClose: () => void
  /** Called after a successful assignment so the parent can refresh its data */
  onSuccess?: () => void
}

// ─── QR Scanner sub-component ─────────────────────────────────────────────────

function QrScannerOverlay({ onScanned, onClose }: { onScanned: (value: string) => void; onClose: () => void }) {
  const [value, setValue] = useState('')
  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[60] p-4">
      <div className="bg-white rounded-2xl p-6 w-full max-w-sm space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="font-semibold text-gray-900">Scan Vehicle Barcode</h3>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded-lg">
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>
        <p className="text-sm text-gray-500">
          Scan the vehicle QR code or enter the barcode value manually.
        </p>
        <input
          autoFocus
          type="text"
          value={value}
          onChange={e => setValue(e.target.value)}
          onKeyDown={e => { if (e.key === 'Enter' && value.trim()) onScanned(value.trim()) }}
          placeholder="Scan or type barcode…"
          className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green/30 focus:border-isdb-green"
        />
        <div className="flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-sm text-gray-600 hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            disabled={!value.trim()}
            onClick={() => onScanned(value.trim())}
            className="flex-1 px-4 py-2 bg-isdb-green text-white rounded-lg text-sm font-medium hover:bg-isdb-green/90 disabled:opacity-50"
          >
            Confirm
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── Main Modal ───────────────────────────────────────────────────────────────

export function AssignVehicleModal({ guest, onClose, onSuccess }: AssignVehicleModalProps) {
  const queryClient = useQueryClient()
  const searchRef = useRef<HTMLInputElement>(null)

  // ── local state ──
  const [search, setSearch] = useState('')
  const [showAll, setShowAll] = useState(false)
  const [filterByClass, setFilterByClass] = useState(false)
  const [selectedVehicleId, setSelectedVehicleId] = useState<string | null>(null)
  const [forcedVehicle, setForcedVehicle] = useState<VehicleOption | null>(null)
  const [assignmentType, setAssignmentType] = useState<'Dedicated' | 'DropOff'>('Dedicated')
  const [destination, setDestination] = useState('')
  const [estimatedArrival, setEstimatedArrival] = useState('')
  const [notes, setNotes] = useState('')
  const [showScanner, setShowScanner] = useState(false)

  // ── data ──
  // Use all-with-status for both views; derive available by filtering status === 'Available'
  const { data: allVehicles = [] } = useQuery<VehicleOption[]>({
    queryKey: ['vehicles', 'all-with-status'],
    queryFn: vehiclesApi.getAllWithStatus,
    refetchInterval: 15_000,
  })
  const availableVehicles = allVehicles.filter(v => v.status === 'Available')

  // Focus search on open
  useEffect(() => { searchRef.current?.focus() }, [])

  // ── derived list ──
  const baseList: VehicleOption[] = showAll ? allVehicles : availableVehicles
  const filtered = baseList.filter(v => {
    if (filterByClass && guest.deservedCarClassName && v.carClassName !== guest.deservedCarClassName) return false
    if (!search) return true
    const q = search.toLowerCase()
    return (
      v.licensePlate?.toLowerCase().includes(q) ||
      v.make?.toLowerCase().includes(q) ||
      v.model?.toLowerCase().includes(q) ||
      v.driverName?.toLowerCase().includes(q) ||
      String(v.carNumber ?? '').toLowerCase().includes(q)
    )
  })

  // ── mutations ──
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['guests'] })
    queryClient.invalidateQueries({ queryKey: ['vehicles'] })
    queryClient.invalidateQueries({ queryKey: ['dashboard'] })
  }

  const assignMutation = useMutation({
    mutationFn: () =>
      vehiclesApi.assign(
        guest.id,
        selectedVehicleId!,
        notes || undefined,
        estimatedArrival || undefined,
        assignmentType,
        destination || undefined,
      ),
    onSuccess: () => {
      invalidate()
      toast.success(`Vehicle assigned to ${guest.fullName}`)
      onSuccess?.()
      onClose()
    },
    onError: () => toast.error('Failed to assign vehicle'),
  })

  const forceMutation = useMutation({
    mutationFn: () =>
      vehiclesApi.forceAssign(
        guest.id,
        forcedVehicle!.id,
        notes || undefined,
        estimatedArrival || undefined,
        assignmentType,
        destination || undefined,
      ),
    onSuccess: (data) => {
      invalidate()
      const msg = data?.displacedGuestName
        ? `Vehicle reassigned from ${data.displacedGuestName} to ${guest.fullName}`
        : `Vehicle assigned to ${guest.fullName}`
      toast.success(msg)
      onSuccess?.()
      onClose()
    },
    onError: () => toast.error('Failed to force-assign vehicle'),
  })

  const barcodeMutation = useMutation({
    mutationFn: (barcode: string) =>
      vehiclesApi.assignByBarcode(guest.id, barcode, notes || undefined),
    onSuccess: () => {
      invalidate()
      toast.success(`Vehicle assigned via barcode to ${guest.fullName}`)
      onSuccess?.()
      onClose()
    },
    onError: () => toast.error('Barcode not found or vehicle unavailable'),
  })

  // ── handlers ──
  const handleSelectVehicle = (v: VehicleOption) => {
    const isAssignedToOther =
      v.status === 'Assigned' && v.currentGuestId && v.currentGuestId !== guest.id
    if (isAssignedToOther) {
      setForcedVehicle(v)
      setSelectedVehicleId(null)
    } else {
      setSelectedVehicleId(v.id)
      setForcedVehicle(null)
    }
  }

  const handleConfirm = () => {
    if (forcedVehicle) {
      forceMutation.mutate()
    } else if (selectedVehicleId) {
      assignMutation.mutate()
    }
  }

  const isPending = assignMutation.isPending || forceMutation.isPending || barcodeMutation.isPending
  const canConfirm = !!(selectedVehicleId || forcedVehicle) && !isPending && (assignmentType !== 'DropOff' || !!destination.trim())

  return (
    <>
      {showScanner && (
        <QrScannerOverlay
          onScanned={barcode => { setShowScanner(false); barcodeMutation.mutate(barcode) }}
          onClose={() => setShowScanner(false)}
        />
      )}

      <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-end sm:items-center justify-center z-50 p-4">
        <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col">

          {/* Header */}
          <div className="flex items-center justify-between p-5 border-b">
            <div>
              <h2 className="text-lg font-semibold text-gray-900">
                {(guest.activeVehiclePlate || guest.activeVehicleId) ? 'Change Vehicle' : 'Assign Vehicle'}
              </h2>
              <p className="text-sm text-gray-500 mt-0.5">{guest.fullName}</p>
              {guest.deservedCarClassName && (
                <span
                  className="inline-flex items-center mt-1 px-2 py-0.5 rounded-full text-xs font-medium text-white"
                  style={{ backgroundColor: guest.deservedCarClassColor ?? '#9CA3AF' }}
                >
                  {guest.deservedCarClassName}
                </span>
              )}
            </div>
            <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Body */}
          <div className="flex-1 overflow-y-auto p-5 space-y-4">

            {/* Force-reassign warning */}
            {forcedVehicle && (
              <div className="p-3 rounded-lg bg-amber-50 border border-amber-300">
                <div className="flex items-start gap-2">
                  <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
                  <div>
                    <p className="text-sm font-semibold text-amber-800">Vehicle Already Assigned</p>
                    <p className="text-xs text-amber-700 mt-0.5">
                      This vehicle is currently assigned to{' '}
                      <strong>{forcedVehicle.currentGuestName ?? 'another guest'}</strong>.
                      Confirming will remove their assignment and assign it to this guest.
                    </p>
                    <button
                      onClick={() => setForcedVehicle(null)}
                      className="text-xs text-amber-600 underline mt-1"
                    >
                      Cancel selection
                    </button>
                  </div>
                </div>
              </div>
            )}

            {/* Search + QR scan */}
            <div className="flex gap-1.5">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" />
                <input
                  ref={searchRef}
                  type="text"
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                  placeholder="Search by plate, car #, make, model, driver…"
                  className="w-full border border-gray-300 rounded-lg pl-8 pr-8 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green/30 focus:border-isdb-green"
                />
                {search && (
                  <button
                    onClick={() => setSearch('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    <X className="w-4 h-4" />
                  </button>
                )}
              </div>
              <button
                type="button"
                onClick={() => setShowScanner(true)}
                title="Scan QR code to find vehicle"
                className="flex-shrink-0 flex items-center justify-center w-9 h-9 rounded-lg border border-gray-300 bg-white hover:bg-isdb-green hover:border-isdb-green hover:text-white text-gray-500 transition-colors"
              >
                <QrCode className="w-4 h-4" />
              </button>
            </div>

            {/* Filter toggles */}
            <div className="flex flex-wrap gap-2">
              <button
                onClick={() => { setShowAll(v => !v); setSearch(''); setForcedVehicle(null) }}
                className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
                  showAll
                    ? 'bg-white text-gray-500 border-gray-300 hover:border-gray-400 hover:text-gray-700'
                    : 'bg-isdb-green text-white border-isdb-green'
                }`}
              >
                {!showAll && <CheckCircle className="w-3 h-3" />}
                {showAll ? 'Show available only' : 'Show available only'}
              </button>
              {guest.deservedCarClassName && (
                <button
                  onClick={() => setFilterByClass(v => !v)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium border transition-colors ${
                    filterByClass
                      ? 'bg-white text-gray-500 border-gray-300 hover:border-gray-400 hover:text-gray-700'
                      : 'bg-isdb-green text-white border-isdb-green'
                  }`}
                >
                  {!filterByClass && <CheckCircle className="w-3 h-3" />}
                  Show {guest.deservedCarClassName} class only
                </button>
              )}
            </div>

            {/* Vehicle list */}
            <div className="space-y-1.5 max-h-[35vh] overflow-y-auto">
              {filtered.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-6">
                  {showAll ? 'No vehicles found.' : 'No available vehicles. Try showing all vehicles.'}
                </p>
              ) : (
                filtered.map(v => {
                  const isSelected = v.id === selectedVehicleId || v.id === forcedVehicle?.id
                  const isAssignedToOther =
                    v.status === 'Assigned' && v.currentGuestId && v.currentGuestId !== guest.id
                  const isCurrent = v.id === guest.activeVehicleId

                  return (
                    <button
                      key={v.id}
                      onClick={() => handleSelectVehicle(v)}
                      className={`w-full text-left p-2.5 rounded-lg border transition-colors ${
                        isSelected
                          ? 'border-isdb-green bg-isdb-green/5'
                          : isAssignedToOther
                          ? 'border-amber-200 bg-amber-50 hover:border-amber-400'
                          : 'border-gray-200 hover:border-gray-300 bg-white'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2 flex-wrap min-w-0">
                          {v.carClassColor && (
                            <span
                              className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                              style={{ backgroundColor: v.carClassColor }}
                              title={v.carClassName ?? ''}
                            />
                          )}
                          <span className="font-medium text-sm text-gray-800 truncate">
                            {v.make} {v.model}
                          </span>
                          <span className="font-mono text-xs text-gray-500">{v.licensePlate}</span>
                          {v.carNumber && (
                            <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono font-semibold bg-orange-50 text-orange-700 border border-orange-200">
                              #{v.carNumber}
                            </span>
                          )}
                          {isCurrent && (
                            <span className="text-[10px] font-medium px-1.5 py-0.5 rounded bg-isdb-green/10 text-isdb-green">
                              Current
                            </span>
                          )}
                        </div>
                        {isAssignedToOther ? (
                          <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-amber-100 text-amber-700 flex-shrink-0 whitespace-nowrap">
                            Assigned
                          </span>
                        ) : (
                          <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-emerald-100 text-emerald-700 flex-shrink-0">
                            Available
                          </span>
                        )}
                      </div>
                      {isAssignedToOther && v.currentGuestName && (
                        <p className="text-xs text-amber-600 mt-0.5 ml-4">→ {v.currentGuestName}</p>
                      )}
                      {v.driverName && (
                        <div className="flex items-center gap-1 mt-0.5 ml-4 text-xs text-gray-500">
                          <User className="w-3 h-3" />
                          {v.driverName}
                        </div>
                      )}
                    </button>
                  )
                })
              )}
            </div>

            {/* Assignment type (only when a vehicle is selected) */}
            {(selectedVehicleId || forcedVehicle) && (
              <div className="space-y-3 pt-2 border-t border-gray-100">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1.5">
                    Assignment Type
                  </label>
                  <div className="flex gap-2">
                    {(['Dedicated', 'DropOff'] as const).map(type => (
                      <button
                        key={type}
                        onClick={() => setAssignmentType(type)}
                        className={`flex-1 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                          assignmentType === type
                            ? 'bg-isdb-green text-white border-isdb-green'
                            : 'bg-white text-gray-600 border-gray-300 hover:border-gray-400'
                        }`}
                      >
                        {type === 'Dedicated' ? 'Dedicated' : 'Drop-off'}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Estimated arrival — only relevant for Dedicated */}
                {assignmentType === 'Dedicated' && (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Estimated Arrival Time <span className="text-gray-400">(optional)</span>
                  </label>
                  <input
                    type="time"
                    value={estimatedArrival}
                    onChange={e => setEstimatedArrival(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green/30 focus:border-isdb-green"
                  />
                </div>
                )}

                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Notes <span className="text-gray-400">(optional)</span>
                  </label>
                  <textarea
                    value={notes}
                    onChange={e => setNotes(e.target.value)}
                    rows={2}
                    placeholder="Any special instructions…"
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green/30 focus:border-isdb-green resize-none"
                  />
                </div>

                {/* Destination — required for Drop-off */}
                {assignmentType === 'DropOff' && (
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">
                      Destination <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={destination}
                      onChange={e => setDestination(e.target.value)}
                      placeholder="e.g. Hotel Hilton, Conference Centre…"
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green/30 focus:border-isdb-green"
                    />
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="p-5 border-t flex gap-3">
            <button
              onClick={onClose}
              className="flex-1 px-4 py-2.5 border border-gray-300 rounded-xl text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleConfirm}
              disabled={!canConfirm}
              className={`flex-1 px-4 py-2.5 rounded-xl text-sm font-medium text-white transition-colors disabled:opacity-50 disabled:cursor-not-allowed ${
                forcedVehicle ? 'bg-amber-600 hover:bg-amber-700' : 'bg-isdb-green hover:bg-isdb-green/90'
              }`}
            >
              {isPending
                ? 'Assigning…'
                : forcedVehicle
                ? 'Confirm Reassignment'
                : (guest.activeVehiclePlate || guest.activeVehicleId)
                ? 'Change Vehicle'
                : 'Assign Vehicle'}
            </button>
          </div>
        </div>
      </div>
    </>
  )
}

export default AssignVehicleModal
