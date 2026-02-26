import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import {
  LayoutDashboard, Users, AlertTriangle, Bell, CheckCircle,
  Plane, Car, Building2, RefreshCw, BellOff,
} from 'lucide-react'
import { dashboardApi, alertsApi } from '../../api/services'
import { AlertSeverity } from '../../types'
import type { Alert } from '../../types'
import { format } from 'date-fns'

const SEVERITY_STYLES: Record<AlertSeverity, string> = {
  [AlertSeverity.Low]: 'bg-blue-50 border-blue-200 text-blue-800',
  [AlertSeverity.Medium]: 'bg-amber-50 border-amber-200 text-amber-800',
  [AlertSeverity.High]: 'bg-orange-50 border-orange-200 text-orange-800',
  [AlertSeverity.Critical]: 'bg-red-50 border-red-200 text-red-800',
}

const SEVERITY_LABELS: Record<AlertSeverity, string> = {
  [AlertSeverity.Low]: 'Low',
  [AlertSeverity.Medium]: 'Medium',
  [AlertSeverity.High]: 'High',
  [AlertSeverity.Critical]: 'Critical',
}

export default function ControlRoomDashboard() {
  const queryClient = useQueryClient()
  const [showAlertForm, setShowAlertForm] = useState(false)
  const [alertTitle, setAlertTitle] = useState('')
  const [alertMessage, setAlertMessage] = useState('')
  const [alertSeverity, setAlertSeverity] = useState<AlertSeverity>(AlertSeverity.Medium)

  const { data: summary, isLoading, refetch } = useQuery({
    queryKey: ['dashboard', 'summary'],
    queryFn: dashboardApi.getSummary,
    refetchInterval: 20_000,
  })

  const resolveAlertMutation = useMutation({
    mutationFn: (alertId: string) => alertsApi.resolve(alertId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success('Alert resolved')
    },
    onError: () => toast.error('Failed to resolve alert'),
  })

  const createAlertMutation = useMutation({
    mutationFn: () => alertsApi.create(alertTitle, alertMessage, alertSeverity),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
      toast.success('Alert created')
      setShowAlertForm(false)
      setAlertTitle('')
      setAlertMessage('')
      setAlertSeverity(AlertSeverity.Medium)
    },
    onError: () => toast.error('Failed to create alert'),
  })

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-isdb-green/10 flex items-center justify-center">
            <LayoutDashboard className="w-5 h-5 text-isdb-green" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Control Room</h1>
            <p className="text-sm text-gray-500">Real-time operations overview</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowAlertForm(true)}
            className="flex items-center gap-2 btn-danger text-sm"
          >
            <Bell className="w-4 h-4" />
            New Alert
          </button>
          <button onClick={() => refetch()} className="btn-secondary p-2">
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {[...Array(8)].map((_, i) => (
            <div key={i} className="card animate-pulse h-24 bg-gray-100" />
          ))}
        </div>
      ) : summary ? (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="card text-center">
              <div className="flex items-center justify-center mb-2">
                <Users className="w-5 h-5 text-gray-400" />
              </div>
              <p className="text-3xl font-bold text-gray-900">{summary.totalGuests}</p>
              <p className="text-sm text-gray-500 mt-1">Total Guests</p>
            </div>
            <div className="card text-center">
              <div className="flex items-center justify-center mb-2">
                <Plane className="w-5 h-5 text-blue-400" />
              </div>
              <p className="text-3xl font-bold text-blue-600">{summary.arrivingCount}</p>
              <p className="text-sm text-gray-500 mt-1">Arriving</p>
            </div>
            <div className="card text-center">
              <div className="flex items-center justify-center mb-2">
                <Car className="w-5 h-5 text-amber-400" />
              </div>
              <p className="text-3xl font-bold text-amber-600">{summary.onTheWayToHotelCount}</p>
              <p className="text-sm text-gray-500 mt-1">In Transit</p>
            </div>
            <div className="card text-center">
              <div className="flex items-center justify-center mb-2">
                <Building2 className="w-5 h-5 text-purple-400" />
              </div>
              <p className="text-3xl font-bold text-purple-600">{summary.atHotelCount}</p>
              <p className="text-sm text-gray-500 mt-1">At Hotel</p>
            </div>
          </div>

          {/* Alert summary */}
          {summary.activeAlertsCount > 0 && (
            <div className={`card border ${summary.criticalAlertsCount > 0 ? 'border-red-300 bg-red-50' : 'border-amber-200 bg-amber-50'}`}>
              <div className="flex items-center gap-2 mb-1">
                <AlertTriangle className={`w-5 h-5 ${summary.criticalAlertsCount > 0 ? 'text-red-600' : 'text-amber-600'}`} />
                <h3 className="font-semibold text-gray-900">
                  {summary.activeAlertsCount} Active Alert{summary.activeAlertsCount !== 1 ? 's' : ''}
                  {summary.criticalAlertsCount > 0 && (
                    <span className="ml-2 badge-critical">{summary.criticalAlertsCount} Critical</span>
                  )}
                </h3>
              </div>
            </div>
          )}

          {/* Active Alerts */}
          {summary.activeAlerts.length > 0 && (
            <div>
              <h2 className="font-semibold text-gray-900 mb-3">Active Alerts</h2>
              <div className="space-y-3">
                {summary.activeAlerts.map((alert: Alert) => (
                  <div
                    key={alert.id}
                    className={`card border ${SEVERITY_STYLES[alert.severity]}`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="font-semibold text-sm">{alert.title}</span>
                          <span className="text-xs px-1.5 py-0.5 rounded bg-white/60 font-medium">
                            {SEVERITY_LABELS[alert.severity]}
                          </span>
                        </div>
                        <p className="text-sm opacity-80">{alert.message}</p>
                        {alert.guestName && (
                          <p className="text-xs opacity-60 mt-1">Guest: {alert.guestName}</p>
                        )}
                        <p className="text-xs opacity-50 mt-1">
                          {format(new Date(alert.createdAt), 'HH:mm, dd MMM yyyy')}
                        </p>
                      </div>
                      <button
                        onClick={() => resolveAlertMutation.mutate(alert.id)}
                        disabled={resolveAlertMutation.isPending}
                        className="flex-shrink-0 flex items-center gap-1 text-xs font-medium px-3 py-1.5 bg-white/70 hover:bg-white rounded-lg transition-colors"
                      >
                        <CheckCircle className="w-3.5 h-3.5" />
                        Resolve
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Guests by status breakdown */}
          <div>
            <h2 className="font-semibold text-gray-900 mb-3">Status Breakdown</h2>
            <div className="grid grid-cols-2 lg:grid-cols-3 gap-3">
              {summary.guestsByStatus.map((group) => (
                <div key={group.statusLabel} className="card">
                  <p className="text-lg font-bold text-gray-900">{group.count}</p>
                  <p className="text-xs text-gray-500 mt-0.5">
                    {group.statusLabel.replace(/([A-Z])/g, ' $1').trim()}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </>
      ) : (
        <div className="card text-center py-12">
          <p className="text-gray-500">Unable to load dashboard data.</p>
        </div>
      )}

      {/* Create alert modal */}
      {showAlertForm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-md p-6">
            <h3 className="font-semibold text-gray-900 mb-4">Create New Alert</h3>
            <div className="space-y-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Title</label>
                <input
                  type="text"
                  value={alertTitle}
                  onChange={(e) => setAlertTitle(e.target.value)}
                  placeholder="Alert title..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Message</label>
                <textarea
                  value={alertMessage}
                  onChange={(e) => setAlertMessage(e.target.value)}
                  placeholder="Alert details..."
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green resize-none"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Severity</label>
                <select
                  value={alertSeverity}
                  onChange={(e) => setAlertSeverity(parseInt(e.target.value) as AlertSeverity)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                >
                  <option value={AlertSeverity.Low}>Low</option>
                  <option value={AlertSeverity.Medium}>Medium</option>
                  <option value={AlertSeverity.High}>High</option>
                  <option value={AlertSeverity.Critical}>Critical</option>
                </select>
              </div>
            </div>
            <div className="flex gap-3 mt-5">
              <button onClick={() => setShowAlertForm(false)} className="flex-1 btn-secondary">
                Cancel
              </button>
              <button
                onClick={() => createAlertMutation.mutate()}
                disabled={!alertTitle || !alertMessage || createAlertMutation.isPending}
                className="flex-1 btn-danger"
              >
                {createAlertMutation.isPending ? 'Creating...' : 'Create Alert'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
