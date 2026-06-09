/**
 * VASummaryPage.tsx
 *
 * Vehicle Allocation Summary dashboard — shown under Transport > VA Summary.
 * Route: transport/va-summary
 * Roles: Transport, Admin, ControlRoom
 *
 * Displays a per-car-class breakdown table with the following columns:
 *  1. Car Class          — name + colour badge
 *  2. Agreement          — vehicles with status Available OR NotProvided
 *  3. Deserved           — active guests whose DeservedCarClassId matches
 *  4. Vehicle Assigned   — vehicles with status Assigned for that class
 *  5. Available Vehicle  — vehicles with status Available for that class
 *  6. Not Provided (Gap) — vehicles with status NotProvided for that class
 *  7. Coverage %         — Assigned ÷ Deserved (visual progress bar)
 *
 * Data source: GET /api/dashboard/summary → DashboardSummaryDto.FleetByClass
 * Auto-refreshes every 60 seconds.
 *
 * NOTE: This file is the readable source reference for the bundle.
 * When making changes, update this file first, then rebuild and inject the bundle.
 */

import { useQuery } from '@tanstack/react-query'
import { RefreshCw, Loader2, LayoutGrid } from 'lucide-react'
import { dashboardApi } from '../../api/services'

// ─────────────────────────────────────────────────────────────────────────────
// Types — mirrors FleetByClassDto from the API (camelCase JSON)
// ─────────────────────────────────────────────────────────────────────────────
interface FleetByClassItem {
  classId: string
  className: string
  classShortName?: string
  classColor?: string
  sortOrder: number
  totalVehicles: number
  available: number
  assigned: number
  outOfService: number
  notProvided: number
  guestsDeserving: number
}

interface VASummaryData {
  fleetByClass?: FleetByClassItem[]
  // Totals used for KPI strip
  vehiclesTotal?: number
  vehiclesAvailable?: number
  vehiclesAssigned?: number
  vehiclesNotProvided?: number
  guestsDeservingVehicle?: number
}

// ─────────────────────────────────────────────────────────────────────────────
// Coverage pill helper
// ─────────────────────────────────────────────────────────────────────────────
function CoveragePill({ assigned, deserved }: { assigned: number; deserved: number }) {
  if (deserved === 0) {
    return <span className="text-xs text-gray-400">—</span>
  }
  const pct = Math.round((assigned / deserved) * 100)
  const barColor =
    pct >= 80 ? 'bg-green-500' : pct >= 50 ? 'bg-amber-400' : 'bg-red-500'
  const textColor =
    pct >= 80 ? 'text-green-700' : pct >= 50 ? 'text-amber-700' : 'text-red-700'

  return (
    <div className="flex items-center gap-2 min-w-[100px]">
      <div className="flex-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full ${barColor}`}
          style={{ width: `${Math.min(pct, 100)}%` }}
        />
      </div>
      <span className={`text-xs font-semibold w-9 text-right tabular-nums ${textColor}`}>
        {pct}%
      </span>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// Gap pill helper
// ─────────────────────────────────────────────────────────────────────────────
function GapPill({ value }: { value: number }) {
  if (value === 0) {
    return (
      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-green-100 text-green-700">
        0
      </span>
    )
  }
  if (value <= 2) {
    return (
      <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-700">
        {value}
      </span>
    )
  }
  return (
    <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-700">
      {value}
    </span>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// KPI card
// ─────────────────────────────────────────────────────────────────────────────
function KpiCard({
  label,
  value,
  color,
}: {
  label: string
  value: number
  color: 'blue' | 'green' | 'purple' | 'amber' | 'red'
}) {
  const valueColors: Record<string, string> = {
    blue: 'text-sky-600',
    green: 'text-green-600',
    purple: 'text-violet-600',
    amber: 'text-amber-600',
    red: 'text-red-600',
  }
  return (
    <div className="bg-white rounded-xl border border-gray-200 px-5 py-4">
      <p className="text-xs font-medium text-gray-500 uppercase tracking-wide mb-1">{label}</p>
      <p className={`text-3xl font-bold tabular-nums ${valueColors[color]}`}>{value}</p>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// Main component
// ─────────────────────────────────────────────────────────────────────────────
export default function VASummaryPage() {
  const {
    data: summary,
    isLoading,
    isError,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['dashboard', 'summary', 'va-summary'],
    queryFn: () => dashboardApi.getSummary() as unknown as Promise<VASummaryData>,
    refetchInterval: 60_000,
  })

  const rows: FleetByClassItem[] = (summary?.fleetByClass ?? [])
    .slice()
    .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))

  // ── KPI totals (sum across all classes) ──────────────────────────────────
  const totalAgreement = rows.reduce((s, r) => s + r.available + r.notProvided, 0)
  const totalDeserved = rows.reduce((s, r) => s + r.guestsDeserving, 0)
  const totalAssigned = rows.reduce((s, r) => s + r.assigned, 0)
  const totalAvailable = rows.reduce((s, r) => s + r.available, 0)
  const totalNotProvided = rows.reduce((s, r) => s + r.notProvided, 0)

  const lastUpdated = dataUpdatedAt
    ? new Date(dataUpdatedAt).toLocaleTimeString('en-GB', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      })
    : '—'

  return (
    <div className="p-4 md:p-6 space-y-5 max-w-7xl mx-auto">
      {/* ── Page header ─────────────────────────────────────────────────── */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-lg bg-sky-100 flex items-center justify-center flex-shrink-0">
            <LayoutGrid className="w-5 h-5 text-sky-600" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-gray-900 leading-tight">
              Vehicle Allocation Summary
            </h1>
            <p className="text-xs text-gray-500">
              Real-time allocation status per car class
            </p>
          </div>
        </div>
        <button
          onClick={() => refetch()}
          disabled={isLoading}
          className="flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 border border-gray-200 rounded-lg bg-white hover:bg-gray-50 disabled:opacity-50 transition-colors"
        >
          {isLoading ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" />
          ) : (
            <RefreshCw className="w-3.5 h-3.5" />
          )}
          Refresh
        </button>
      </div>

      {/* ── Error state ─────────────────────────────────────────────────── */}
      {isError && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Failed to load summary data. Please refresh.
        </div>
      )}

      {/* ── KPI strip ───────────────────────────────────────────────────── */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        <KpiCard label="Agreement" value={totalAgreement} color="blue" />
        <KpiCard label="Deserved (Guests)" value={totalDeserved} color="purple" />
        <KpiCard label="Vehicle Assigned" value={totalAssigned} color="amber" />
        <KpiCard label="Available" value={totalAvailable} color="green" />
        <KpiCard label="Not Provided (Gap)" value={totalNotProvided} color="red" />
      </div>

      {/* ── Main table ──────────────────────────────────────────────────── */}
      <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
        {/* Table header bar */}
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <div className="w-2 h-2 rounded-full bg-sky-500" />
            <span className="text-sm font-semibold text-gray-800">Allocation by Car Class</span>
          </div>
          <span className="text-xs text-gray-400">Last updated: {lastUpdated}</span>
        </div>

        {/* Loading skeleton */}
        {isLoading && (
          <div className="p-6 space-y-3">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-10 rounded-lg bg-gray-100 animate-pulse" />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!isLoading && rows.length === 0 && (
          <div className="p-10 text-center">
            <LayoutGrid className="w-10 h-10 text-gray-300 mx-auto mb-2" />
            <p className="text-sm text-gray-500">No car classes configured yet.</p>
          </div>
        )}

        {/* Table */}
        {!isLoading && rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-gray-50 border-b border-gray-100">
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    Car Class
                  </th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <span className="block">Agreement</span>
                    <span className="text-gray-400 normal-case font-normal tracking-normal">
                      (Avail + Not Provided)
                    </span>
                  </th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <span className="block">Deserved</span>
                    <span className="text-gray-400 normal-case font-normal tracking-normal">
                      (Guests)
                    </span>
                  </th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <span className="block">Vehicle</span>
                    <span className="text-gray-400 normal-case font-normal tracking-normal">
                      Assigned
                    </span>
                  </th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <span className="block">Available</span>
                    <span className="text-gray-400 normal-case font-normal tracking-normal">
                      Vehicles
                    </span>
                  </th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <span className="block">Not Provided</span>
                    <span className="text-gray-400 normal-case font-normal tracking-normal">
                      (Gap)
                    </span>
                  </th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide min-w-[140px]">
                    Coverage
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {rows.map((row) => {
                  const agreement = row.available + row.notProvided
                  return (
                    <tr key={row.classId} className="hover:bg-gray-50 transition-colors">
                      {/* Car Class */}
                      <td className="px-4 py-3.5">
                        <div className="flex items-center gap-2.5">
                          <div
                            className="w-2.5 h-2.5 rounded-full flex-shrink-0"
                            style={{ backgroundColor: row.classColor ?? '#9ca3af' }}
                          />
                          <span className="font-semibold text-gray-800">{row.className}</span>
                        </div>
                      </td>
                      {/* Agreement */}
                      <td className="px-4 py-3.5 text-center">
                        <span className="font-bold text-sky-600 tabular-nums text-base">
                          {agreement}
                        </span>
                      </td>
                      {/* Deserved */}
                      <td className="px-4 py-3.5 text-center">
                        <span className="font-bold text-violet-600 tabular-nums text-base">
                          {row.guestsDeserving}
                        </span>
                      </td>
                      {/* Vehicle Assigned */}
                      <td className="px-4 py-3.5 text-center">
                        <span className="font-bold text-amber-600 tabular-nums text-base">
                          {row.assigned}
                        </span>
                      </td>
                      {/* Available */}
                      <td className="px-4 py-3.5 text-center">
                        <span className="font-bold text-green-600 tabular-nums text-base">
                          {row.available}
                        </span>
                      </td>
                      {/* Not Provided */}
                      <td className="px-4 py-3.5 text-center">
                        <GapPill value={row.notProvided} />
                      </td>
                      {/* Coverage */}
                      <td className="px-4 py-3.5">
                        <CoveragePill
                          assigned={row.assigned}
                          deserved={row.guestsDeserving}
                        />
                      </td>
                    </tr>
                  )
                })}
              </tbody>
              {/* Totals footer */}
              <tfoot>
                <tr className="bg-gray-50 border-t-2 border-gray-200">
                  <td className="px-4 py-3 font-bold text-gray-800">Total</td>
                  <td className="px-4 py-3 text-center font-bold text-gray-800 tabular-nums">
                    {totalAgreement}
                  </td>
                  <td className="px-4 py-3 text-center font-bold text-gray-800 tabular-nums">
                    {totalDeserved}
                  </td>
                  <td className="px-4 py-3 text-center font-bold text-gray-800 tabular-nums">
                    {totalAssigned}
                  </td>
                  <td className="px-4 py-3 text-center font-bold text-gray-800 tabular-nums">
                    {totalAvailable}
                  </td>
                  <td className="px-4 py-3 text-center font-bold text-gray-800 tabular-nums">
                    {totalNotProvided}
                  </td>
                  <td className="px-4 py-3" />
                </tr>
              </tfoot>
            </table>
          </div>
        )}

        {/* Legend */}
        {!isLoading && rows.length > 0 && (
          <div className="flex flex-wrap gap-x-6 gap-y-1.5 px-5 py-3 border-t border-gray-100 bg-gray-50">
            <div className="flex items-center gap-1.5 text-xs text-gray-500">
              <div className="w-2 h-2 rounded-full bg-sky-500" />
              Agreement = Available + Not Provided vehicles
            </div>
            <div className="flex items-center gap-1.5 text-xs text-gray-500">
              <div className="w-2 h-2 rounded-full bg-violet-500" />
              Deserved = Active guests with this car class
            </div>
            <div className="flex items-center gap-1.5 text-xs text-gray-500">
              <div className="w-2 h-2 rounded-full bg-amber-500" />
              Coverage = Assigned ÷ Deserved
            </div>
            <div className="flex items-center gap-1.5 text-xs text-gray-500">
              <div className="w-2 h-2 rounded-full bg-red-500" />
              Not Provided = Contracted but not yet on site
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
