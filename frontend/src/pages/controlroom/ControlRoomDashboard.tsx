/**
 * ControlRoomDashboard.tsx
 *
 * Full Control Room Dashboard — Annual Meetings 2026.
 * Accessible to Admin and ControlRoom roles.
 *
 * Sections:
 *  1. Reception Overview
 *  2. Arrivals by Day
 *  3. Vehicle Allocation
 *  4. Car Class Inventory
 *  5. Guest–Vehicle Coverage
 *  6. Accommodation
 *  7. Departure Forms
 *  8. User Activity
 *  9. Additional Metrics
 */

import { useState, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  BarChart, Bar, PieChart, Pie, Cell,
  XAxis, YAxis, Tooltip, Legend, ResponsiveContainer,
  CartesianGrid,
} from 'recharts'
import { RefreshCw } from 'lucide-react'
import { dashboardApi, departureApi } from '../../api/services'
import type {
  HotelGuestCount,
  DayArrival,
  ActivityFeedItem,
  HourlyActivity,
} from '../../api/services'
import type { FleetClassSummary } from '../../types'

// ─── Colour palette ───────────────────────────────────────────────────────────
const C = {
  green:  '#22c55e',
  blue:   '#3b82f6',
  amber:  '#f59e0b',
  red:    '#ef4444',
  indigo: '#6366f1',
  teal:   '#14b8a6',
  gray:   '#94a3b8',
  purple: '#a855f7',
  rose:   '#f43f5e',
} as const

type AccentKey = keyof typeof C

// ─── Shared helpers ───────────────────────────────────────────────────────────
function pct(num: number, denom: number) {
  if (!denom) return 0
  return Math.round((num / denom) * 100)
}

function fmtRelative(iso: string) {
  const diff = Date.now() - new Date(iso).getTime()
  const mins = Math.floor(diff / 60_000)
  if (mins < 1)  return 'just now'
  if (mins < 60) return `${mins} min ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24)  return `${hrs} h ago`
  return `${Math.floor(hrs / 24)} d ago`
}

// ─── KPI Card ─────────────────────────────────────────────────────────────────
interface KpiCardProps {
  label: string
  value: number | string
  sub?: string
  accent?: AccentKey
  progress?: number
  badge?: string
  badgeColor?: string
}
function KpiCard({ label, value, sub, accent = 'indigo', progress, badge, badgeColor }: KpiCardProps) {
  const color = C[accent]
  return (
    <div
      className="bg-white rounded-xl border border-gray-200 p-4 flex flex-col gap-1 shadow-sm"
      style={{ borderLeftWidth: 4, borderLeftColor: color }}
    >
      <div className="text-[11px] font-semibold text-gray-500 uppercase tracking-wide">{label}</div>
      <div className="text-3xl font-bold text-gray-900">{value}</div>
      {sub && <div className="text-xs text-gray-500">{sub}</div>}
      {progress !== undefined && (
        <div className="mt-1 h-1.5 rounded-full bg-gray-100 overflow-hidden">
          <div className="h-full rounded-full transition-all" style={{ width: `${progress}%`, backgroundColor: color }} />
        </div>
      )}
      {badge && (
        <span
          className="mt-1 inline-flex self-start items-center px-2 py-0.5 rounded-full text-[10px] font-semibold"
          style={{ background: badgeColor ? `${badgeColor}20` : '#fef3c7', color: badgeColor ?? '#92400e' }}
        >
          {badge}
        </span>
      )}
    </div>
  )
}

// ─── Section Title ────────────────────────────────────────────────────────────
function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2 mt-8 mb-4">
      <div className="h-px flex-1 bg-gray-200" />
      <h2 className="text-xs font-bold text-gray-500 uppercase tracking-widest px-3">{children}</h2>
      <div className="h-px flex-1 bg-gray-200" />
    </div>
  )
}

// ─── Chart Card ───────────────────────────────────────────────────────────────
function ChartCard({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
      <h3 className="text-sm font-semibold text-gray-800">{title}</h3>
      {subtitle && <p className="text-xs text-gray-500 mt-0.5 mb-3">{subtitle}</p>}
      {children}
    </div>
  )
}

// ─── Loading Skeleton ─────────────────────────────────────────────────────────
function Skeleton({ className }: { className?: string }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className ?? 'h-8 w-full'}`} />
}

// ─── Activity type colours ────────────────────────────────────────────────────
const activityTypeColors: Record<string, { bg: string; text: string; avatar: string }> = {
  Vehicle: { bg: '#dbeafe', text: '#1d4ed8', avatar: '#3b82f6' },
  Hotel:   { bg: '#dcfce7', text: '#15803d', avatar: '#22c55e' },
  Guest:   { bg: '#f3e8ff', text: '#7e22ce', avatar: '#a855f7' },
  Arrival: { bg: '#fef3c7', text: '#92400e', avatar: '#f59e0b' },
  Default: { bg: '#f1f5f9', text: '#475569', avatar: '#94a3b8' },
}

const hotelGradients = [
  'linear-gradient(135deg,#1e3a5f 0%,#3b82f6 100%)',
  'linear-gradient(135deg,#1e3a5f 0%,#7c3aed 100%)',
  'linear-gradient(135deg,#1e3a5f 0%,#0f766e 100%)',
  'linear-gradient(135deg,#1e3a5f 0%,#b45309 100%)',
  'linear-gradient(135deg,#1e3a5f 0%,#be185d 100%)',
]

// ─── Main Component ───────────────────────────────────────────────────────────
const ControlRoomDashboard = () => {
  const [refreshKey, setRefreshKey] = useState(0)
  const refresh = useCallback(() => setRefreshKey(k => k + 1), [])

  // ── Data fetching ─────────────────────────────────────────────────────────
  const { data: summary, isLoading: loadingSummary } = useQuery({
    queryKey: ['dashboard-summary', refreshKey],
    queryFn:  dashboardApi.getSummary,
    staleTime: 60_000,
  })

  const { data: hotelSummary, isLoading: loadingHotel } = useQuery({
    queryKey: ['hotel-summary', refreshKey],
    queryFn:  dashboardApi.getHotelSummary,
    staleTime: 60_000,
  })

  const { data: arrivalsByDay, isLoading: loadingArrivals } = useQuery({
    queryKey: ['arrivals-by-day', refreshKey],
    queryFn:  dashboardApi.getArrivalsByDay,
    staleTime: 60_000,
  })

  const { data: userActivity, isLoading: loadingActivity } = useQuery({
    queryKey: ['user-activity', refreshKey],
    queryFn:  dashboardApi.getUserActivity,
    staleTime: 30_000,
  })

  const { data: departureStats, isLoading: loadingDeparture } = useQuery({
    queryKey: ['departure-stats', refreshKey],
    queryFn:  departureApi.getStats,
    staleTime: 60_000,
  })

  const isLoading = loadingSummary || loadingHotel || loadingArrivals || loadingActivity || loadingDeparture

  // ── Derived data ──────────────────────────────────────────────────────────
  const totalGuests   = summary?.totalGuests ?? 0
  const arrivedCount  = (summary?.arrivingCount ?? 0) +
                        (summary?.receivedByEmbassyCount ?? 0) +
                        (summary?.onTheWayToHotelCount ?? 0) +
                        (summary?.atHotelCount ?? 0) +
                        (summary?.departingCount ?? 0)
  const enRoute       = summary?.onTheWayToHotelCount ?? summary?.inTransit ?? 0
  const atHotel       = summary?.atHotelCount ?? summary?.atHotel ?? 0
  const notYetArrived = Math.max(0, totalGuests - arrivedCount)
  const receivedByEmb = summary?.receivedByEmbassyCount ?? 0

  const totalVehicles = summary?.vehiclesTotal ??
    ((summary?.vehiclesAvailable ?? 0) + (summary?.vehiclesAssigned ?? 0) +
     (summary?.vehiclesOutOfService ?? 0) + (summary?.vehiclesNotProvided ?? 0))
  const available    = summary?.vehiclesAvailable ?? 0
  const assigned     = summary?.vehiclesAssigned ?? 0
  const outOfService = summary?.vehiclesOutOfService ?? 0

  const fleetByClass: FleetClassSummary[] = summary?.fleetByClass ?? []

  const fleetDonutData = [
    { name: 'Available',      value: available,    fill: C.green  },
    { name: 'Assigned',       value: assigned,     fill: C.amber  },
    { name: 'Out of Service', value: outOfService, fill: C.red    },
    { name: 'Not Provided',   value: summary?.vehiclesNotProvided ?? 0, fill: C.gray },
  ].filter(d => d.value > 0)

  const guestsDeserving = summary?.guestsDeservingVehicle ?? summary?.guestsDeservingCarClass ?? 0
  const guestsCovered   = assigned
  const guestsWaiting   = Math.max(0, guestsDeserving - guestsCovered)
  const guestVsCarData  = [
    { name: 'With Vehicle',    value: guestsCovered, fill: C.green },
    { name: 'Without Vehicle', value: guestsWaiting, fill: C.red   },
  ].filter(d => d.value > 0)

  const arrivalDays: DayArrival[] = arrivalsByDay?.days ?? []

  const depTotal   = departureStats?.totalRegistrations ?? 0
  const depByHotel = departureStats?.byHotel ?? []
  const depByDay   = (departureStats?.byDay ?? []).map(d => ({ day: d.dayLabel, count: d.count }))

  const actFeed: ActivityFeedItem[]  = userActivity?.recentFeed ?? []
  const hourlyBreakdown: HourlyActivity[] = userActivity?.hourlyBreakdown ?? []
  const actByType = [
    { name: 'Guest Updates', value: userActivity?.guestUpdates ?? 0,    fill: C.purple },
    { name: 'Assignments',   value: userActivity?.assignments ?? 0,     fill: C.blue   },
    { name: 'Hotel Updates', value: userActivity?.hotelUpdates ?? 0,    fill: C.green  },
    { name: 'Arrivals',      value: userActivity?.markedAsArrived ?? 0, fill: C.amber  },
  ].filter(d => d.value > 0)

  const byHotel: HotelGuestCount[] = hotelSummary?.byHotel ?? []
  const totalCheckedIn = hotelSummary?.totalAtHotel ?? 0
  const noRoomAssigned = hotelSummary?.noRoomAssigned ?? 0

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <div className="bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between sticky top-0 z-10 shadow-sm">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-lg bg-indigo-600 flex items-center justify-center text-white font-bold text-sm">
            AM
          </div>
          <div>
            <div className="font-bold text-gray-900 text-base leading-tight">Control Room Dashboard</div>
            <div className="text-xs text-gray-500">Annual Meetings 2026 · Baku</div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 text-xs font-medium text-green-600 bg-green-50 border border-green-200 rounded-full px-3 py-1">
            <span className="w-2 h-2 rounded-full bg-green-500 animate-pulse" />
            Live
          </div>
          <button
            onClick={refresh}
            disabled={isLoading}
            className="flex items-center gap-1.5 text-xs font-medium text-gray-600 bg-white border border-gray-300 rounded-lg px-3 py-1.5 hover:bg-gray-50 disabled:opacity-50 transition-colors"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>
      </div>

      <div className="max-w-screen-2xl mx-auto px-6 py-6">

        {/* ── SECTION 1: RECEPTION OVERVIEW ── */}
        <SectionTitle>Reception Overview</SectionTitle>
        {loadingSummary ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Array.from({ length: 7 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <KpiCard label="Total Guests"        value={totalGuests}   sub="Registered for 2026"        accent="indigo" />
              <KpiCard label="Arrived (Cumulative)" value={arrivedCount}  sub={`${pct(arrivedCount, totalGuests)}% of total`} accent="green" progress={pct(arrivedCount, totalGuests)} />
              <KpiCard label="En Route to Hotel"    value={enRoute}       sub="In transit right now"       accent="amber"  />
              <KpiCard label="At Hotel"             value={atHotel}       sub="Checked in"                 accent="blue"   />
            </div>
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4 mt-4">
              <KpiCard label="Not Yet Arrived"      value={notYetArrived} sub="Pending arrival"            accent="gray"   />
              <KpiCard label="Received by Embassy"  value={receivedByEmb} sub="Handed over to embassy team" accent="teal"  />
              <KpiCard label="Departing Active"     value={summary?.departingCount ?? 0} sub="Departure journey started" accent="rose" />
            </div>
          </>
        )}

        {/* ── SECTION 2: ARRIVALS BY DAY ── */}
        <SectionTitle>Arrivals by Day</SectionTitle>
        {loadingArrivals ? (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Skeleton className="h-64" /><Skeleton className="h-64" />
          </div>
        ) : arrivalDays.length === 0 ? (
          <div className="bg-white rounded-xl border border-gray-200 p-8 text-center text-gray-400 text-sm">
            No arrival data available yet.
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <ChartCard title="Scheduled vs. Arrived per Day" subtitle="Grouped by flight arrival date">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={arrivalDays} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Bar dataKey="scheduled" name="Scheduled" fill={C.indigo} radius={[3,3,0,0]} />
                  <Bar dataKey="arrived"   name="Arrived"   fill={C.green}  radius={[3,3,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>
            <ChartCard title="Daily Arrival Breakdown" subtitle="Arrived vs. pending for each day">
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={arrivalDays} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="dateLabel" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                  <Bar dataKey="arrived" name="Arrived" stackId="a" fill={C.green} />
                  <Bar dataKey="pending" name="Pending" stackId="a" fill={C.amber} radius={[3,3,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>
          </div>
        )}

        {/* ── SECTION 3: VEHICLE ALLOCATION ── */}
        <SectionTitle>Vehicle Allocation</SectionTitle>
        {loadingSummary ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <KpiCard label="Total Vehicles"  value={totalVehicles} sub="Across all car classes"  accent="indigo" />
              <KpiCard label="Available"        value={available}     sub="Ready for assignment"    accent="green"  />
              <KpiCard label="Assigned"         value={assigned}      sub="Currently with a guest"  accent="amber"  />
              <KpiCard label="Out of Service"   value={outOfService}  sub="Unavailable"             accent="red"    />
            </div>
            {fleetByClass.length > 0 && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                <ChartCard title="Fleet Status by Car Class" subtitle="Available / Assigned / Out of Service per class">
                  <ResponsiveContainer width="100%" height={220}>
                    <BarChart
                      data={fleetByClass.map(c => ({
                        name:             c.className,
                        Available:        c.available,
                        Assigned:         c.assigned,
                        'Out of Service': c.outOfService ?? 0,
                        'Not Provided':   c.notProvided ?? 0,
                      }))}
                      margin={{ top: 8, right: 8, left: -10, bottom: 0 }}
                    >
                      <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                      <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                      <YAxis tick={{ fontSize: 11 }} />
                      <Tooltip />
                      <Legend wrapperStyle={{ fontSize: 11 }} />
                      <Bar dataKey="Available"      fill={C.green}  radius={[0,0,0,0]} />
                      <Bar dataKey="Assigned"       fill={C.amber}  radius={[0,0,0,0]} />
                      <Bar dataKey="Out of Service" fill={C.red}    radius={[0,0,0,0]} />
                      <Bar dataKey="Not Provided"   fill={C.gray}   radius={[3,3,0,0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </ChartCard>
                <ChartCard title="Fleet Status Overview" subtitle="Overall distribution across all vehicles">
                  <ResponsiveContainer width="100%" height={220}>
                    <PieChart>
                      <Pie
                        data={fleetDonutData}
                        cx="50%" cy="50%"
                        innerRadius={55} outerRadius={85}
                        paddingAngle={3} dataKey="value"
                      >
                        {fleetDonutData.map((entry, i) => <Cell key={i} fill={entry.fill} />)}
                      </Pie>
                      <Tooltip />
                      <Legend wrapperStyle={{ fontSize: 11 }} />
                    </PieChart>
                  </ResponsiveContainer>
                </ChartCard>
              </div>
            )}
          </>
        )}

        {/* ── SECTION 4: CAR CLASS INVENTORY TABLE ── */}
        {fleetByClass.length > 0 && (
          <>
            <SectionTitle>Car Class Inventory</SectionTitle>
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="bg-gray-50 border-b border-gray-200">
                      {['Car Class','Guests Deserving','Not Provided','Available','Assigned','Out of Service','Total','Utilisation'].map(h => (
                        <th key={h} className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide first:text-left [&:not(:first-child)]:text-right">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {fleetByClass.map((cls, i) => {
                      const tot  = cls.totalVehicles ?? cls.total
                      const util = tot > 0 ? pct(cls.assigned, tot) : 0
                      return (
                        <tr key={i} className="hover:bg-gray-50 transition-colors">
                          <td className="px-4 py-3 font-medium text-gray-800">
                            <div className="flex items-center gap-2">
                              {cls.classColor && <span className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: cls.classColor }} />}
                              {cls.className}
                            </div>
                          </td>
                          <td className="px-4 py-3 text-right font-semibold text-indigo-600">{cls.guestsDeserving ?? 0}</td>
                          <td className="px-4 py-3 text-right text-gray-600">{cls.notProvided ?? 0}</td>
                          <td className="px-4 py-3 text-right text-green-600 font-medium">{cls.available}</td>
                          <td className="px-4 py-3 text-right text-amber-600 font-medium">{cls.assigned}</td>
                          <td className="px-4 py-3 text-right text-red-500">{cls.outOfService ?? 0}</td>
                          <td className="px-4 py-3 text-right font-semibold text-gray-800">{tot}</td>
                          <td className="px-4 py-3 text-right">
                            <div className="flex items-center justify-end gap-2">
                              <div className="w-16 h-1.5 rounded-full bg-gray-100 overflow-hidden">
                                <div className="h-full rounded-full" style={{ width: `${util}%`, backgroundColor: util > 80 ? C.red : util > 50 ? C.amber : C.green }} />
                              </div>
                              <span className="text-xs text-gray-600 w-8 text-right">{util}%</span>
                            </div>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}

        {/* ── SECTION 5: GUEST–VEHICLE COVERAGE ── */}
        <SectionTitle>Guest–Vehicle Coverage</SectionTitle>
        {loadingSummary ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-56" />)}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <ChartCard title="Vehicle Assignment Status" subtitle="Guests with vs. without an active vehicle assignment">
              {guestVsCarData.length > 0 ? (
                <ResponsiveContainer width="100%" height={200}>
                  <PieChart>
                    <Pie data={guestVsCarData} cx="50%" cy="50%" innerRadius={50} outerRadius={75} paddingAngle={4} dataKey="value">
                      {guestVsCarData.map((entry, i) => <Cell key={i} fill={entry.fill} />)}
                    </Pie>
                    <Tooltip />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                  </PieChart>
                </ResponsiveContainer>
              ) : (
                <div className="h-48 flex items-center justify-center text-gray-400 text-sm">No data</div>
              )}
            </ChartCard>
            <ChartCard title="Coverage by Car Class" subtitle="For each class: guests covered vs. waiting">
              {fleetByClass.length > 0 ? (
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={fleetByClass.map(c => ({ name: c.className, Covered: c.assigned, Waiting: Math.max(0, (c.guestsDeserving ?? 0) - c.assigned) }))} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                    <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                    <YAxis tick={{ fontSize: 11 }} />
                    <Tooltip />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                    <Bar dataKey="Covered" stackId="a" fill={C.green} />
                    <Bar dataKey="Waiting" stackId="a" fill={C.red} radius={[3,3,0,0]} />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="h-48 flex items-center justify-center text-gray-400 text-sm">No data</div>
              )}
            </ChartCard>
            <ChartCard title="Cars Needed to Close the Gap" subtitle="Available vehicles vs. additional cars required per class">
              {fleetByClass.length > 0 ? (
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={fleetByClass.map(c => ({ name: c.className, Available: c.available, Needed: Math.max(0, (c.guestsDeserving ?? 0) - c.assigned - c.available) }))} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                    <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                    <YAxis tick={{ fontSize: 11 }} />
                    <Tooltip />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                    <Bar dataKey="Available" fill={C.green} radius={[3,3,0,0]} />
                    <Bar dataKey="Needed"    fill={C.red}   radius={[3,3,0,0]} />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="h-48 flex items-center justify-center text-gray-400 text-sm">No data</div>
              )}
            </ChartCard>
          </div>
        )}

        {/* ── SECTION 6: ACCOMMODATION ── */}
        <SectionTitle>Accommodation</SectionTitle>
        {loadingHotel ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-40" />)}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4 mb-4">
              <KpiCard label="Total Checked In" value={totalCheckedIn} sub="Across all hotels" accent="blue" />
              <KpiCard
                label="No Room Number"
                value={noRoomAssigned}
                sub="Room not yet assigned"
                accent="amber"
                badge={noRoomAssigned > 0 ? '⚠ Follow up' : undefined}
                badgeColor="#f59e0b"
              />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {byHotel.map((hotel, i) => (
                <div key={i} className="rounded-xl overflow-hidden border border-gray-200 shadow-sm">
                  <div className="p-4 text-white" style={{ background: hotelGradients[i % hotelGradients.length] }}>
                    <div className="text-[10px] uppercase tracking-widest opacity-70 mb-1">Hotel</div>
                    <div className="flex items-center justify-between">
                      <h4 className="font-bold text-base">{hotel.hotelName}</h4>
                      <div className="text-right">
                        <div className="text-2xl font-bold">{hotel.guestCount}</div>
                        <div className="text-[10px] opacity-70">Total Checked In</div>
                      </div>
                    </div>
                  </div>
                  <div className="grid grid-cols-2 divide-x divide-gray-100 bg-white">
                    <div className="p-3 text-center">
                      <div className="text-xl font-bold text-green-600">{hotel.withRoomCount ?? 0}</div>
                      <div className="text-[11px] text-gray-500 mt-0.5">With Room #</div>
                    </div>
                    <div className="p-3 text-center">
                      <div className="text-xl font-bold text-amber-600">{hotel.noRoomCount ?? 0}</div>
                      <div className="text-[11px] text-gray-500 mt-0.5">No Room #</div>
                    </div>
                  </div>
                </div>
              ))}
              {byHotel.length === 0 && (
                <div className="col-span-3 bg-white rounded-xl border border-gray-200 p-8 text-center text-gray-400 text-sm">
                  No hotel check-in data available yet.
                </div>
              )}
            </div>
          </>
        )}

        {/* ── SECTION 7: DEPARTURE FORMS ── */}
        <SectionTitle>Departure Forms</SectionTitle>
        {loadingDeparture ? (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <KpiCard label="Total Submissions" value={depTotal} sub="Across all hotels" accent="indigo" />
              {depByHotel.slice(0, 3).map((h, i) => (
                <KpiCard
                  key={i}
                  label={h.hotelName}
                  value={h.count}
                  sub="Submissions received"
                  accent={(['green', 'purple', 'teal'] as AccentKey[])[i % 3]}
                />
              ))}
            </div>
            {depByDay.length > 0 && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                <ChartCard title="Submissions by Day" subtitle="Total departure form submissions received each day">
                  <ResponsiveContainer width="100%" height={220}>
                    <BarChart data={depByDay} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                      <XAxis dataKey="day" tick={{ fontSize: 11 }} />
                      <YAxis tick={{ fontSize: 11 }} />
                      <Tooltip />
                      <Bar dataKey="count" name="Submissions" fill={C.indigo} radius={[3,3,0,0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </ChartCard>
                <ChartCard title="Submissions by Hotel" subtitle="Number of submissions per hotel">
                  <ResponsiveContainer width="100%" height={220}>
                    <PieChart>
                      <Pie
                        data={depByHotel.map((h, i) => ({ name: h.hotelName, value: h.count }))}
                        cx="50%" cy="50%"
                        outerRadius={80} dataKey="value"
                        label
                      >
                        {depByHotel.map((_, i) => (
                          <Cell key={i} fill={[C.indigo, C.green, C.purple, C.teal, C.amber][i % 5]} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </ChartCard>
              </div>
            )}
          </>
        )}

        {/* ── SECTION 8: USER ACTIVITY ── */}
        <SectionTitle>User Activity</SectionTitle>
        {loadingActivity ? (
          <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
            {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-24" />)}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
              <KpiCard label="Active Users"      value={userActivity?.activeUsers ?? 0}     sub="Logged in today"         accent="indigo" />
              <KpiCard label="Total Actions"     value={userActivity?.totalActions ?? 0}    sub="Last 24 h"               accent="green"  />
              <KpiCard label="Guest Updates"     value={userActivity?.guestUpdates ?? 0}    sub="Profile changes"         accent="amber"  />
              <KpiCard label="Assignments"       value={userActivity?.assignments ?? 0}     sub="Vehicle assignments"     accent="blue"   />
              <KpiCard label="Hotel Updates"     value={userActivity?.hotelUpdates ?? 0}    sub="Room & check-in changes" accent="teal"   />
              <KpiCard label="Marked as Arrived" value={userActivity?.markedAsArrived ?? 0} sub="Arrival confirmed today" accent="rose"   />
            </div>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
              <ChartCard title="Actions by Type" subtitle="Breakdown of system actions in the last 24 hours">
                {actByType.length > 0 ? (
                  <ResponsiveContainer width="100%" height={200}>
                    <PieChart>
                      <Pie data={actByType} cx="50%" cy="50%" innerRadius={45} outerRadius={70} paddingAngle={4} dataKey="value">
                        {actByType.map((entry, i) => <Cell key={i} fill={entry.fill} />)}
                      </Pie>
                      <Tooltip />
                      <Legend wrapperStyle={{ fontSize: 11 }} />
                    </PieChart>
                  </ResponsiveContainer>
                ) : (
                  <div className="h-48 flex items-center justify-center text-gray-400 text-sm">No activity yet</div>
                )}
              </ChartCard>
              <div className="md:col-span-2">
                <ChartCard title="Recent Activity Feed" subtitle="Latest actions by users on the system">
                  {actFeed.length === 0 ? (
                    <div className="h-48 flex items-center justify-center text-gray-400 text-sm">No recent activity</div>
                  ) : (
                    <div className="space-y-3 max-h-56 overflow-y-auto pr-1">
                      {actFeed.map((item, i) => {
                        const colors = activityTypeColors[item.type] ?? activityTypeColors.Default
                        return (
                          <div key={i} className="flex items-start gap-3">
                            <div className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold flex-shrink-0" style={{ backgroundColor: colors.avatar }}>
                              {item.actorInitials}
                            </div>
                            <div className="flex-1 min-w-0">
                              <div className="flex items-center gap-2 flex-wrap">
                                <span className="text-xs font-semibold text-gray-800">{item.actorName}</span>
                                <span className="text-[10px] font-semibold px-1.5 py-0.5 rounded-full" style={{ background: colors.bg, color: colors.text }}>{item.type}</span>
                              </div>
                              <div className="text-xs text-gray-600 mt-0.5">
                                {item.action}{item.guestName && <> for <strong>{item.guestName}</strong></>}
                              </div>
                              <div className="text-[10px] text-gray-400 mt-0.5">{fmtRelative(item.occurredAt)}</div>
                            </div>
                          </div>
                        )
                      })}
                    </div>
                  )}
                </ChartCard>
              </div>
            </div>
            {hourlyBreakdown.length > 0 && (
              <div className="mt-4">
                <ChartCard title="User Actions Over Time (Today)" subtitle="Hourly breakdown of system activity by all users">
                  <ResponsiveContainer width="100%" height={180}>
                    <BarChart data={hourlyBreakdown} margin={{ top: 8, right: 8, left: -10, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                      <XAxis dataKey="hourLabel" tick={{ fontSize: 9 }} interval={2} />
                      <YAxis tick={{ fontSize: 11 }} />
                      <Tooltip />
                      <Bar dataKey="count" name="Actions" fill={C.indigo} radius={[3,3,0,0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </ChartCard>
              </div>
            )}
          </>
        )}

        {/* ── SECTION 9: ADDITIONAL METRICS ── */}
        <SectionTitle>Additional Metrics</SectionTitle>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <ChartCard title="Departure Readiness" subtitle="Guests with confirmed departure flight, vehicle, and hotel checkout — vs. those with gaps">
            {loadingSummary ? (
              <Skeleton className="h-40" />
            ) : (
              <div className="space-y-3 mt-2">
                {[
                  { label: 'Fully departed',          value: summary?.departingCount ?? 0,                                color: C.green  },
                  { label: 'At hotel (not departed)',  value: Math.max(0, atHotel - (summary?.departingCount ?? 0)),       color: C.amber  },
                  { label: 'Without vehicle',          value: summary?.guestsWithoutVehicle ?? 0,                         color: C.red    },
                  { label: 'En route to hotel',        value: enRoute,                                                    color: C.blue   },
                ].map((row, i) => (
                  <div key={i} className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: row.color }} />
                      <span className="text-gray-700">{row.label}</span>
                    </div>
                    <span className="font-bold" style={{ color: row.color }}>{row.value}</span>
                  </div>
                ))}
              </div>
            )}
          </ChartCard>

          {fleetByClass.length > 0 && (
            <ChartCard title="Fleet Utilisation by Class" subtitle="Percentage of vehicles assigned vs. total per class">
              <ResponsiveContainer width="100%" height={180}>
                <BarChart
                  data={fleetByClass.map(c => ({ name: c.className, util: c.totalVehicles ? pct(c.assigned, c.totalVehicles) : 0 }))}
                  margin={{ top: 8, right: 8, left: -10, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                  <YAxis tick={{ fontSize: 11 }} domain={[0, 100]} unit="%" />
                  <Tooltip formatter={(v: unknown) => `${v}%`} />
                  <Bar dataKey="util" name="Utilisation" fill={C.indigo} radius={[3,3,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            </ChartCard>
          )}
        </div>

        <div className="h-12" />
      </div>
    </div>
  )
}

export default ControlRoomDashboard
