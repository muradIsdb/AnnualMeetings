import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Plane, Search, Users, AlertTriangle, CheckCircle } from 'lucide-react'
import { guestsApi } from '../../api/services'
import { GuestStatus } from '../../types'
import GuestCard from '../../components/ui/GuestCard'

const ARRIVAL_STATUSES = [
  GuestStatus.Expected,
  GuestStatus.ArrivedAtAirport,
  GuestStatus.PassedPassportControl,
  GuestStatus.LuggageReceived,
  GuestStatus.ReceivedByEmbassy,
]

export default function AirportDashboard() {
  const [search, setSearch] = useState('')
  const [filterStatus, setFilterStatus] = useState<GuestStatus | 'all'>('all')

  const { data: guests = [], isLoading, refetch } = useQuery({
    queryKey: ['guests', 'airport'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30_000,
  })

  const arrivalGuests = guests.filter((g) =>
    ARRIVAL_STATUSES.includes(parseInt(g.statusLabel) as GuestStatus) ||
    ARRIVAL_STATUSES.some((s) => GuestStatus[s] === g.statusLabel)
  )

  const filtered = arrivalGuests.filter((g) => {
    const matchSearch = !search ||
      g.fullName.toLowerCase().includes(search.toLowerCase()) ||
      (g.designation?.toLowerCase().includes(search.toLowerCase()) ?? false)
    return matchSearch
  })

  const expectedCount = guests.filter((g) => g.statusLabel === 'Expected').length
  const arrivedCount = guests.filter((g) =>
    ['ArrivedAtAirport', 'PassedPassportControl', 'LuggageReceived'].includes(g.statusLabel)
  ).length
  const completedCount = guests.filter((g) => g.statusLabel === 'ReceivedByEmbassy').length
  const criticalCount = guests.filter((g) => g.isCritical).length

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-blue-100 flex items-center justify-center">
            <Plane className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Airport Reception</h1>
            <p className="text-sm text-gray-500">Arrival queue management</p>
          </div>
        </div>
        <button onClick={() => refetch()} className="btn-secondary text-sm">
          Refresh
        </button>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="card text-center">
          <p className="text-2xl font-bold text-gray-900">{expectedCount}</p>
          <p className="text-sm text-gray-500 mt-1">Expected</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-blue-600">{arrivedCount}</p>
          <p className="text-sm text-gray-500 mt-1">In Process</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-green-600">{completedCount}</p>
          <p className="text-sm text-gray-500 mt-1">Received</p>
        </div>
        <div className="card text-center">
          <p className="text-2xl font-bold text-red-600">{criticalCount}</p>
          <p className="text-sm text-gray-500 mt-1">VIP Critical</p>
        </div>
      </div>

      {/* Search */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search guests by name or designation..."
          className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
        />
      </div>

      {/* Guest List */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-gray-900">
            Arrival Queue
            <span className="ml-2 text-sm font-normal text-gray-500">({filtered.length} guests)</span>
          </h2>
        </div>

        {isLoading ? (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="card animate-pulse h-20 bg-gray-100" />
            ))}
          </div>
        ) : filtered.length === 0 ? (
          <div className="card text-center py-12">
            <CheckCircle className="w-12 h-12 text-green-400 mx-auto mb-3" />
            <p className="text-gray-500">No guests in the arrival queue.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {filtered.map((guest) => (
              <GuestCard
                key={guest.id}
                guest={guest}
                linkTo={`/airport/guest/${guest.id}`}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
