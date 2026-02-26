import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { Building2, Search, CheckCircle, QrCode, Copy } from 'lucide-react'
import { guestsApi } from '../../api/services'
import { GuestStatus } from '../../types'
import type { GuestSummary } from '../../types'
import GuestCard from '../../components/ui/GuestCard'

const HOTEL_STATUSES = ['AtHotel', 'DepartingHotel', 'OnTheWayToHotel']

export default function HotelDashboard() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [selectedGuest, setSelectedGuest] = useState<GuestSummary | null>(null)
  const [showQrModal, setShowQrModal] = useState(false)

  const { data: guests = [], isLoading } = useQuery({
    queryKey: ['guests', 'hotel'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30_000,
  })

  const updateStatusMutation = useMutation({
    mutationFn: ({ guestId, status }: { guestId: string; status: GuestStatus }) =>
      guestsApi.updateStatus(guestId, status),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['guests'] })
      toast.success('Guest status updated')
      setSelectedGuest(null)
    },
    onError: () => toast.error('Failed to update status'),
  })

  const hotelGuests = guests.filter((g) => HOTEL_STATUSES.includes(g.statusLabel))
  const filtered = hotelGuests.filter((g) =>
    !search || g.fullName.toLowerCase().includes(search.toLowerCase())
  )

  const atHotelCount = guests.filter((g) => g.statusLabel === 'AtHotel').length
  const arrivingCount = guests.filter((g) => g.statusLabel === 'OnTheWayToHotel').length
  const departingCount = guests.filter((g) => g.statusLabel === 'DepartingHotel').length

  const departureFormUrl = `${window.location.origin}/departure-request`

  const copyDepartureLink = () => {
    navigator.clipboard.writeText(departureFormUrl)
    toast.success('Departure request link copied!')
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-purple-100 flex items-center justify-center">
            <Building2 className="w-5 h-5 text-purple-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Hotel Management</h1>
            <p className="text-sm text-gray-500">Accommodation & departure coordination</p>
          </div>
        </div>
        <button
          onClick={() => setShowQrModal(true)}
          className="flex items-center gap-2 btn-secondary text-sm"
        >
          <QrCode className="w-4 h-4" />
          Departure Form
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card text-center">
          <p className="text-2xl font-bold text-blue-600">{arrivingCount}</p>
          <p className="text-sm text-gray-500 mt-1">En Route</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-purple-600">{atHotelCount}</p>
          <p className="text-sm text-gray-500 mt-1">At Hotel</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-amber-600">{departingCount}</p>
          <p className="text-sm text-gray-500 mt-1">Departing</p>
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
          Hotel Guests
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
            <p className="text-gray-500">No hotel guests at this time.</p>
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

      {/* Guest action modal */}
      {selectedGuest && (
        <div className="fixed inset-0 bg-black/50 flex items-end sm:items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm p-6">
            <h3 className="font-semibold text-gray-900 mb-1">{selectedGuest.fullName}</h3>
            <p className="text-sm text-gray-500 mb-5">Update accommodation status</p>
            <div className="space-y-2">
              <button
                onClick={() => updateStatusMutation.mutate({ guestId: selectedGuest.id, status: GuestStatus.AtHotel })}
                disabled={updateStatusMutation.isPending}
                className="w-full btn-primary"
              >
                Checked In to Hotel
              </button>
              <button
                onClick={() => updateStatusMutation.mutate({ guestId: selectedGuest.id, status: GuestStatus.DepartingHotel })}
                disabled={updateStatusMutation.isPending}
                className="w-full btn-secondary"
              >
                Departing Hotel
              </button>
              <button
                onClick={() => setSelectedGuest(null)}
                className="w-full text-sm text-gray-500 hover:text-gray-700 py-2"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* QR / Departure form modal */}
      {showQrModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-sm p-6">
            <h3 className="font-semibold text-gray-900 mb-2">Guest Departure Request Form</h3>
            <p className="text-sm text-gray-500 mb-4">
              Share this link with hotel guests so they can submit their own departure transport requests.
            </p>
            <div className="bg-gray-50 rounded-lg p-3 text-xs text-gray-600 break-all mb-4 font-mono">
              {departureFormUrl}
            </div>
            <div className="flex gap-3">
              <button onClick={() => setShowQrModal(false)} className="flex-1 btn-secondary">
                Close
              </button>
              <button onClick={copyDepartureLink} className="flex-1 btn-primary flex items-center justify-center gap-2">
                <Copy className="w-4 h-4" />
                Copy Link
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
