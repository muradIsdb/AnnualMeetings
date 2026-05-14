/**
 * TransportRoleDashboard.tsx
 *
 * Landing dashboard for the Transport role.
 * Displays real-time fleet statistics (vehicles, drivers, dispatch status)
 * and a fleet-by-class breakdown — mirroring the Fleet Management page cards.
 */

import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import {
  Car,
  Users,
  Truck,
  AlertTriangle,
  RefreshCw,
  Loader2,
} from 'lucide-react'
import { dashboardApi } from '../../api/services'
import { vehiclesApi } from '../../api/services'

// ─── Stat Card ────────────────────────────────────────────────────────────────

interface StatCardProps {
  icon: React.ElementType
  value: number | string
  label: string
  sublabel?: string
  variant?: 'default' | 'amber' | 'green' | 'blue' | 'red'
  onClick?: () => void
}

function StatCard({ icon: Icon, value, label, sublabel, variant = 'default', onClick }: StatCardProps) {
  const variantStyles: Record<string, { card: string; icon: string; value: string }> = {
    default: {
      card: 'border-gray-200 bg-white',
      icon: 'text-gray-400',
      value: 'text-gray-900',
    },
    blue: {
      card: 'border-blue-100 bg-white',
      icon: 'text-blue-400',
      value: 'text-blue-600',
    },
    amber: {
      card: 'border-amber-200 bg-amber-50',
      icon: 'text-amber-400',
      value: 'text-amber-600',
    },
    green: {
      card: 'border-green-200 bg-green-50',
      icon: 'text-green-400',
      value: 'text-green-600',
    },
    red: {
      card: 'border-red-200 bg-red-50',
      icon: 'text-red-400',
      value: 'text-red-600',
    },
  }

  const s = variantStyles[variant]

  return (
    <div
      className={`rounded-2xl border p-5 flex flex-col items-center gap-2 text-center ${s.card} ${onClick ? 'cursor-pointer hover:shadow-md transition-shadow' : ''}`}
      onClick={onClick}
    >
      <Icon className={`w-7 h-7 ${s.icon}`} />
      <span className={`text-3xl font-bold ${s.value}`}>{value}</span>
      <div>
        <p className="text-sm font-medium text-gray-700">{label}</p>
        {sublabel && <p className="text-xs text-gray-400 mt-0.5">{sublabel}</p>}
      </div>
    </div>
  )
}

// ─── Fleet Class Card ─────────────────────────────────────────────────────────

interface FleetClassCardProps {
  className: string
  color?: string
  available: number
  assigned: number
  guests: number
}

function FleetClassCard({ className: name, color, available, assigned, guests }: FleetClassCardProps) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4">
      <div className="flex items-center gap-2 mb-3">
        <span
          className="w-2.5 h-2.5 rounded-full flex-shrink-0"
          style={{ backgroundColor: color ?? '#6b7280' }}
        />
        <span className="text-sm font-semibold text-gray-800 truncate">{name}</span>
      </div>
      <div className="grid grid-cols-3 gap-1 text-center">
        <div>
          <p className={`text-lg font-bold ${available > 0 ? 'text-isdb-green' : 'text-gray-400'}`}>{available}</p>
          <p className="text-[10px] text-gray-400">Avail</p>
        </div>
        <div>
          <p className={`text-lg font-bold ${assigned > 0 ? 'text-amber-500' : 'text-gray-400'}`}>{assigned}</p>
          <p className="text-[10px] text-gray-400">Asgnd</p>
        </div>
        <div>
          <p className={`text-lg font-bold ${guests > 0 ? 'text-red-500' : 'text-gray-400'}`}>{guests}</p>
          <p className="text-[10px] text-gray-400">Guests</p>
        </div>
      </div>
    </div>
  )
}

// ─── Main Dashboard ───────────────────────────────────────────────────────────

export default function TransportRoleDashboard() {
  const navigate = useNavigate()

  const {
    data: summary,
    isLoading: summaryLoading,
    refetch: refetchSummary,
    isFetching: summaryFetching,
  } = useQuery({
    queryKey: ['dashboard', 'summary', 'transport-role'],
    queryFn: dashboardApi.getSummary,
    refetchInterval: 30_000,
  })

  const { data: allVehicles = [] } = useQuery({
    queryKey: ['vehicles', 'all-with-status', 'transport-role'],
    queryFn: vehiclesApi.getAllWithStatus,
    refetchInterval: 30_000,
  })

  // Guests assigned to a vehicle but without a dedicated car class assignment
  const assignedWithoutDedicatedCar = allVehicles.filter(
    (v) => v.currentGuestId && !v.carClassId
  ).length

  const isLoading = summaryLoading

  return (
    <div className="p-4 md:p-6 max-w-5xl mx-auto">
      {/* ── Header ── */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Transport Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Real-time fleet and dispatch overview</p>
        </div>
        <button
          onClick={() => refetchSummary()}
          disabled={summaryFetching}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${summaryFetching ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center h-48 text-gray-400">
          <Loader2 className="w-8 h-8 animate-spin" />
        </div>
      ) : (
        <>
          {/* ── Top Stats Grid ── */}
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-6">
            <StatCard
              icon={Truck}
              value={summary?.vehiclesAvailable ?? 0}
              label="Vehicles Available"
              sublabel={`of ${(summary?.vehiclesAvailable ?? 0) + (summary?.vehiclesAssigned ?? 0)} total`}
              variant="default"
            />
            <StatCard
              icon={Car}
              value={summary?.vehiclesAssigned ?? 0}
              label="Vehicles Assigned"
              sublabel="0 out of service"
              variant="default"
            />
            <StatCard
              icon={Users}
              value={summary?.driversAvailable ?? 0}
              label="Drivers Available"
              sublabel="of fleet total"
              variant="default"
            />
            <StatCard
              icon={Users}
              value={summary?.guestsDeservingCarClass ?? 0}
              label="Guests Deserving"
              sublabel="car class assigned"
              variant="blue"
            />
            <StatCard
              icon={AlertTriangle}
              value={summary?.guestsWithoutVehicle ?? 0}
              label="Without Vehicle"
              sublabel="need assignment"
              variant={(summary?.guestsWithoutVehicle ?? 0) > 0 ? 'amber' : 'default'}
              onClick={() => navigate('/transport')}
            />
            <StatCard
              icon={AlertTriangle}
              value={assignedWithoutDedicatedCar}
              label="Assigned w/o Ded. Car"
              sublabel={assignedWithoutDedicatedCar === 0 ? 'no anomalies' : 'needs review'}
              variant={assignedWithoutDedicatedCar > 0 ? 'red' : 'green'}
            />
          </div>

          {/* ── Fleet by Class ── */}
          {summary?.fleetByClass && summary.fleetByClass.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-3">
                Fleet by Class
              </p>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                {summary.fleetByClass.map((cls) => (
                  <FleetClassCard
                    key={cls.className}
                    className={cls.className}
                    available={cls.available}
                    assigned={cls.assigned}
                    guests={cls.guests}
                  />
                ))}
              </div>
            </div>
          )}

          {/* ── Quick Actions ── */}
          <div className="mt-6 flex flex-wrap gap-3">
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
