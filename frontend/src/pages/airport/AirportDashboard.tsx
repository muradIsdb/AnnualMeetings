import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plane, Search, CheckCircle, RefreshCw, Download, Tag, Loader2 } from 'lucide-react'
import { guestsApi } from '../../api/services'
import { GuestStatus } from '../../types'
import GuestCard from '../../components/ui/GuestCard'
import api from '../../api/client'

const ARRIVAL_STATUSES = [
  GuestStatus.Expected,
  GuestStatus.ArrivedAtAirport,
  GuestStatus.PassedPassportControl,
  GuestStatus.LuggageReceived,
  GuestStatus.ReceivedByEmbassy,
]

interface SyncJobStatus {
  jobId: string
  state: 'running' | 'done' | 'error'
  message: string
  added: number
  updated: number
  totalFetched: number
  startedAt: string
  finishedAt?: string
}

async function startSync(): Promise<{ jobId: string; message: string; selectedTypeCount: number }> {
  const response = await api.post('/guests/sync-from-eventsair')
  return response.data
}

async function pollSyncStatus(jobId: string): Promise<SyncJobStatus> {
  const response = await api.get(`/guests/sync-status/${jobId}`)
  return response.data
}

export default function AirportDashboard() {
  const [search, setSearch] = useState('')
  const [syncBanner, setSyncBanner] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null)
  const [activeJobId, setActiveJobId] = useState<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const queryClient = useQueryClient()

  const { data: guests = [], isLoading, refetch } = useQuery({
    queryKey: ['guests', 'airport'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30_000,
  })

  // Poll sync job status
  useEffect(() => {
    if (!activeJobId) return

    const poll = async () => {
      try {
        const status = await pollSyncStatus(activeJobId)
        if (status.state === 'done') {
          clearInterval(pollRef.current!)
          setActiveJobId(null)
          setSyncBanner({ type: 'success', text: status.message })
          queryClient.invalidateQueries({ queryKey: ['guests'] })
          setTimeout(() => setSyncBanner(null), 8000)
        } else if (status.state === 'error') {
          clearInterval(pollRef.current!)
          setActiveJobId(null)
          setSyncBanner({ type: 'error', text: status.message })
          setTimeout(() => setSyncBanner(null), 10000)
        }
      } catch {
        // ignore transient poll errors
      }
    }

    pollRef.current = setInterval(poll, 3000)
    return () => { if (pollRef.current) clearInterval(pollRef.current) }
  }, [activeJobId, queryClient])

  const syncMutation = useMutation({
    mutationFn: startSync,
    onSuccess: (data) => {
      setActiveJobId(data.jobId)
      setSyncBanner({ type: 'info', text: `Syncing participants from EventsAir… This may take a minute.` })
    },
    onError: (err: unknown) => {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
        ?? 'Failed to start sync. Please try again.'
      setSyncBanner({ type: 'error', text: msg })
      setTimeout(() => setSyncBanner(null), 8000)
    },
  })

  const isSyncing = syncMutation.isPending || !!activeJobId

  const arrivalGuests = guests.filter((g) =>
    ARRIVAL_STATUSES.includes(parseInt(g.statusLabel) as GuestStatus) ||
    ARRIVAL_STATUSES.some((s) => GuestStatus[s] === g.statusLabel)
  )

  const filtered = arrivalGuests.filter((g) => {
    const matchSearch = !search ||
      g.fullName.toLowerCase().includes(search.toLowerCase()) ||
      (g.designation?.toLowerCase().includes(search.toLowerCase()) ?? false) ||
      (g.registrationTypeName?.toLowerCase().includes(search.toLowerCase()) ?? false)
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
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-blue-100 flex items-center justify-center">
            <Plane className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Airport Reception</h1>
            <p className="text-sm text-gray-500">Arrival queue management</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => refetch()}
            disabled={isSyncing}
            className="btn-secondary text-sm flex items-center gap-1.5 disabled:opacity-50"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Refresh
          </button>
          <button
            onClick={() => syncMutation.mutate()}
            disabled={isSyncing}
            className="btn-primary text-sm flex items-center gap-1.5 disabled:opacity-60"
          >
            {isSyncing
              ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
              : <Download className="w-3.5 h-3.5" />}
            {isSyncing ? 'Syncing…' : 'Sync from EventsAir'}
          </button>
        </div>
      </div>

      {/* Sync feedback banner */}
      {syncBanner && (
        <div className={`rounded-lg px-4 py-3 text-sm font-medium flex items-center gap-2 ${
          syncBanner.type === 'success'
            ? 'bg-green-50 text-green-800 border border-green-200'
            : syncBanner.type === 'error'
            ? 'bg-red-50 text-red-800 border border-red-200'
            : 'bg-blue-50 text-blue-800 border border-blue-200'
        }`}>
          {syncBanner.type === 'success' ? (
            <CheckCircle className="w-4 h-4 flex-shrink-0" />
          ) : syncBanner.type === 'info' ? (
            <Loader2 className="w-4 h-4 flex-shrink-0 animate-spin" />
          ) : (
            <span className="w-4 h-4 flex-shrink-0">✕</span>
          )}
          {syncBanner.text}
        </div>
      )}

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
          placeholder="Search by name, designation, or registration type…"
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
            <p className="text-gray-500">
              {guests.length === 0
                ? 'No participants loaded yet. Click "Sync from EventsAir" to load participants.'
                : 'No guests in the arrival queue.'}
            </p>
            {guests.length === 0 && (
              <button
                onClick={() => syncMutation.mutate()}
                disabled={isSyncing}
                className="btn-primary mt-4 text-sm flex items-center gap-1.5 mx-auto disabled:opacity-60"
              >
                {isSyncing
                  ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  : <Download className="w-3.5 h-3.5" />}
                {isSyncing ? 'Syncing…' : 'Sync from EventsAir'}
              </button>
            )}
          </div>
        ) : (
          <div className="space-y-3">
            {filtered.map((guest) => (
              <div key={guest.id} className="relative">
                <GuestCard
                  guest={guest}
                  linkTo={`/airport/guest/${guest.id}`}
                />
                {guest.registrationTypeName && (
                  <div className="absolute top-2 right-2 flex items-center gap-1 bg-indigo-50 text-indigo-700 text-xs font-medium px-2 py-0.5 rounded-full border border-indigo-100 pointer-events-none">
                    <Tag className="w-3 h-3" />
                    {guest.registrationTypeName}
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
