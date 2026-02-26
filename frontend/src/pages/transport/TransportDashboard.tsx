import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Car, QrCode, Search, X, CheckCircle } from 'lucide-react'
import { guestsApi, vehiclesApi } from '../../api/services'
import { GuestStatus } from '../../types'
import type { GuestSummary, Vehicle } from '../../types'
import GuestCard from '../../components/ui/GuestCard'

export default function TransportDashboard() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [selectedGuest, setSelectedGuest] = useState<GuestSummary | null>(null)
  const [barcodeInput, setBarcodeInput] = useState('')
  const [showBarcodeModal, setShowBarcodeModal] = useState(false)

  const { data: guests = [], isLoading } = useQuery({
    queryKey: ['guests', 'transport'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30_000,
  })

  const { data: vehicles = [] } = useQuery({
    queryKey: ['vehicles', 'available'],
    queryFn: vehiclesApi.getAvailable,
    refetchInterval: 15_000,
  })

  const assignVehicleMutation = useMutation({
    mutationFn: ({ vehicleId }: { vehicleId: string }) =>
      vehiclesApi.assign(selectedGuest!.id, vehicleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['guests'] })
      queryClient.invalidateQueries({ queryKey: ['vehicles'] })
      toast.success(`Vehicle assigned to ${selectedGuest?.fullName}`)
      setSelectedGuest(null)
    },
    onError: () => toast.error('Failed to assign vehicle'),
  })

  const assignByBarcodeMutation = useMutation({
    mutationFn: () =>
      vehiclesApi.assignByBarcode(selectedGuest!.id, barcodeInput),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['guests'] })
      queryClient.invalidateQueries({ queryKey: ['vehicles'] })
      toast.success(`Vehicle assigned via barcode to ${selectedGuest?.fullName}`)
      setSelectedGuest(null)
      setShowBarcodeModal(false)
      setBarcodeInput('')
    },
    onError: () => toast.error('Barcode not found or vehicle unavailable'),
  })

  const transitGuests = guests.filter((g) =>
    ['ReceivedByEmbassy', 'OnTheWayToHotel', 'DepartingHotel', 'AtAirportDeparture'].includes(g.statusLabel)
  )

  const filtered = transitGuests.filter((g) =>
    !search || g.fullName.toLowerCase().includes(search.toLowerCase())
  )

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-amber-100 flex items-center justify-center">
          <Car className="w-5 h-5 text-amber-600" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-gray-900">Transportation</h1>
          <p className="text-sm text-gray-500">Vehicle dispatch & assignment</p>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-4">
        <div className="card text-center">
          <p className="text-2xl font-bold text-amber-600">{transitGuests.length}</p>
          <p className="text-sm text-gray-500 mt-1">Guests in Transit</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-green-600">{vehicles.length}</p>
          <p className="text-sm text-gray-500 mt-1">Available Vehicles</p>
        </div>
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search guests..."
          className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
        />
      </div>

      {/* Guest list */}
      <div>
        <h2 className="font-semibold text-gray-900 mb-3">
          Guests Requiring Transport
          <span className="ml-2 text-sm font-normal text-gray-500">({filtered.length})</span>
        </h2>
        {isLoading ? (
          <div className="space-y-3">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="card animate-pulse h-20 bg-gray-100" />
            ))}
          </div>
        ) : filtered.length === 0 ? (
          <div className="card text-center py-10">
            <CheckCircle className="w-10 h-10 text-green-400 mx-auto mb-2" />
            <p className="text-gray-500">No guests awaiting transport.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {filtered.map((guest) => (
              <GuestCard
                key={guest.id}
                guest={guest}
                onSelect={setSelectedGuest}
              />
            ))}
          </div>
        )}
      </div>

      {/* Vehicle assignment modal */}
      {selectedGuest && (
        <div className="fixed inset-0 bg-black/50 flex items-end sm:items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-md max-h-[80vh] overflow-y-auto">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <div>
                <h3 className="font-semibold text-gray-900">Assign Vehicle</h3>
                <p className="text-sm text-gray-500">{selectedGuest.fullName}</p>
              </div>
              <button onClick={() => setSelectedGuest(null)} className="p-1 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5 text-gray-500" />
              </button>
            </div>

            <div className="p-5 space-y-3">
              {/* Barcode scan option */}
              <button
                onClick={() => setShowBarcodeModal(true)}
                className="w-full flex items-center gap-3 p-3 border-2 border-dashed border-isdb-green/30 rounded-xl hover:border-isdb-green hover:bg-isdb-green/5 transition-colors"
              >
                <QrCode className="w-5 h-5 text-isdb-green" />
                <span className="text-sm font-medium text-isdb-green">Scan Vehicle Barcode</span>
              </button>

              <p className="text-xs text-gray-400 text-center">— or select from available vehicles —</p>

              {vehicles.length === 0 ? (
                <p className="text-center text-sm text-gray-500 py-4">No vehicles available.</p>
              ) : (
                vehicles.map((vehicle: Vehicle) => (
                  <button
                    key={vehicle.id}
                    onClick={() => assignVehicleMutation.mutate({ vehicleId: vehicle.id })}
                    disabled={assignVehicleMutation.isPending}
                    className="w-full flex items-center gap-3 p-3 border border-gray-200 rounded-xl hover:border-isdb-green hover:bg-isdb-green/5 transition-colors text-left"
                  >
                    <div className="w-9 h-9 rounded-lg bg-gray-100 flex items-center justify-center flex-shrink-0">
                      <Car className="w-4 h-4 text-gray-600" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-gray-900">{vehicle.make} {vehicle.model}</p>
                      <p className="text-xs text-gray-500">{vehicle.licensePlate} · {vehicle.driverName}</p>
                    </div>
                  </button>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {/* Barcode input modal */}
      {showBarcodeModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm p-6">
            <h3 className="font-semibold text-gray-900 mb-4">Enter Vehicle Barcode</h3>
            <input
              type="text"
              value={barcodeInput}
              onChange={(e) => setBarcodeInput(e.target.value)}
              placeholder="Scan or type barcode..."
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green mb-4"
              autoFocus
              onKeyDown={(e) => e.key === 'Enter' && assignByBarcodeMutation.mutate()}
            />
            <div className="flex gap-3">
              <button
                onClick={() => { setShowBarcodeModal(false); setBarcodeInput('') }}
                className="flex-1 btn-secondary"
              >
                Cancel
              </button>
              <button
                onClick={() => assignByBarcodeMutation.mutate()}
                disabled={!barcodeInput || assignByBarcodeMutation.isPending}
                className="flex-1 btn-primary"
              >
                Assign
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
