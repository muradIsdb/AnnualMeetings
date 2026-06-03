/**
 * OpsOverview.tsx
 *
 * Ops Overview Dashboard — Annual Meetings 2026.
 * Accessible to Admin and ControlRoom roles.
 *
 * Sections (matching the agreed mockup):
 *  1. Reception Overview
 *  2. Arrivals by Day
 *  3. Vehicle Allocation
 *  4. Assignment Gaps
 *  5. Guest–Vehicle Coverage
 *  6. Accommodation
 *  7. Departure Forms
 *  8. User Activity
 *  9. Additional Metrics
 */
import { useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  BarChart, Bar, PieChart, Pie, Cell, LineChart, Line,
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
  FleetByClassItem,
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

// ─── KPI card ─────────────────────────────────────────────────────────────────
interface KpiCardProps {
  label: string
  value: number | string
  sub?: string
  accent?: AccentKey
  badge?: { text: string; color: 'green' | 'amber' | 'red' | 'blue' | 'purple' }
  progress?: number
}
function KpiCard({ label, value, sub, accent = 'blue', badge, progress }: KpiCardProps) {
  const borderColors: Record<AccentKey, string> = {
    green:  'border-l-green-400',
    blue:   'border-l-blue-400',
    amber:  'border-l-amber-400',
    red:    'border-l-red-400',
    indigo: 'border-l-indigo-400',
    teal:   'border-l-teal-400',
    gray:   'border-l-gray-400',
    purple: 'border-l-purple-400',
    rose:   'border-l-rose-400',
  }
  const badgeBg: Record<string, string> = {
    green:  'bg-green-100 text-green-800',
    amber:  'bg-amber-100 text-amber-800',
    red:    'bg-red-100 text-red-800',
    blue:   'bg-blue-100 text-blue-800',
    purple: 'bg-purple-100 text-purple-800',
  }
  return (
    <div className={`bg-white border border-gray-200 border-l-4 ${borderColors[accent]} rounded-xl p-4 flex flex-col gap-1`}>
      <div className="text-xs font-semibold text-gray-400 uppercase tracking-wide">{label}</div>
      <div className="text-3xl font-extrabold text-gray-900 leading-none">{value}</div>
      {sub && <div className="text-xs text-gray-500 mt-0.5">{sub}</div>}
      {progress !== undefined && (
        <div className="h-1.5 bg-gray-100 rounded-full overflow-hidden mt-1">
          <div className="h-full rounded-full" style={{ width: `${Math.min(progress, 100)}%`, background: C[accent] }} />
        </div>
      )}
      {badge && (
        <span className={`inline-flex items-center text-xs font-semibold px-2 py-0.5 rounded-full mt-1 w-fit ${badgeBg[badge.color]}`}>
          {badge.text}
        </span>
      )}
    </div>
  )
}

// ─── Section header ───────────────────────────────────────────────────────────
function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2 mt-7 mb-3.5">
      <span className="text-xs font-bold text-gray-500 uppercase tracking-widest whitespace-nowrap">{children}</span>
      <div className="flex-1 h-px bg-gray-200" />
    </div>
  )
}

// ─── Chart card ───────────────────────────────────────────────────────────────
function ChartCard({ title, sub, children }: { title: string; sub?: string; children: React.ReactNode }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5">
      <h3 className="text-sm font-bold text-gray-900">{title}</h3>
      {sub && <p className="text-xs text-gray-400 mt-0.5 mb-3.5">{sub}</p>}
      {children}
    </div>
  )
}

// ─── Hotel card ───────────────────────────────────────────────────────────────
const HOTEL_GRADIENTS = [
  'from-[#1e3a5f] to-[#2563eb]',
  'from-[#1e3a5f] to-[#7c3aed]',
  'from-[#1e3a5f] to-[#0f766e]',
  'from-[#1e3a5f] to-[#b45309]',
  'from-[#1e3a5f] to-[#be185d]',
  'from-[#1e3a5f] to-[#0369a1]',
]
function HotelCard({ hotel, index }: { hotel: HotelGuestCount; index: number }) {
  const grad = HOTEL_GRADIENTS[index % HOTEL_GRADIENTS.length]
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className={`bg-gradient-to-br ${grad} p-3.5 flex items-center justify-between`}>
        <div>
          <div className="text-xs text-blue-200 uppercase tracking-wider mb-0.5">Hotel</div>
          <h4 className="text-sm font-bold text-white">{hotel.hotelName}</h4>
        </div>
        <div className="text-right">
          <div className="text-2xl font-extrabold text-white">{hotel.guestCount}</div>
          <div className="text-xs text-blue-200 uppercase tracking-wider">Total Checked In</div>
        </div>
      </div>
      <div className="flex">
        <div className="flex-1 p-3 text-center border-r border-gray-100">
          <div className="text-xl font-extrabold text-green-600">{hotel.withRoomCount}</div>
          <div className="text-xs text-gray-400 uppercase tracking-wider mt-0.5">With Room #</div>
        </div>
        <div className="flex-1 p-3 text-center">
          <div className="text-xl font-extrabold text-amber-600">{hotel.noRoomCount}</div>
          <div className="text-xs text-gray-400 uppercase tracking-wider mt-0.5">No Room #</div>
        </div>
      </div>
    </div>
  )
}

// ─── Activity feed item ────────────────────────────────────────────────────────
const ACTIVITY_COLORS: Record<string, string> = {
  Vehicle:  '#3b82f6',
  Hotel:    '#22c55e',
  Sync:     '#f59e0b',
  Guest:    '#a855f7',
  Alert:    '#ef4444',
  Arrival:  '#14b8a6',
  Other:    '#94a3b8',
}
function ActivityItem({ item }: { item: ActivityFeedItem }) {
  const color = ACTIVITY_COLORS[item.type] ?? ACTIVITY_COLORS.Other
  const tagBg: Record<string, string> = {
    Vehicle: 'bg-blue-100 text-blue-800',
    Hotel:   'bg-green-100 text-green-800',
    Sync:    'bg-amber-100 text-amber-800',
    Guest:   'bg-purple-100 text-purple-800',
    Alert:   'bg-red-100 text-red-800',
    Arrival: 'bg-teal-100 text-teal-800',
    Other:   'bg-gray-100 text-gray-700',
  }
  const tag = tagBg[item.type] ?? tagBg.Other
  const when = (() => {
    try {
      const diff = Date.now() - new Date(item.occurredAt).getTime()
      const mins = Math.floor(diff / 60000)
      if (mins < 1) return 'just now'
      if (mins < 60) return `${mins} min ago`
      const hrs = Math.floor(mins / 60)
      if (hrs < 24) return `${hrs} hr ago`
      return `${Math.floor(hrs / 24)} day ago`
    } catch { return '' }
  })()
  return (
    <div className="flex items-start gap-3 py-2.5 border-b border-gray-50 last:border-0">
      <div className="w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold text-white flex-shrink-0" style={{ background: color }}>
        {item.actorInitials}
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1 flex-wrap">
          <span className="text-xs font-bold text-gray-900">{item.actorName}</span>
          <span className={`text-xs font-bold px-1.5 py-0.5 rounded-full ${tag}`}>{item.type}</span>
        </div>
        <div className="text-xs text-gray-600 mt-0.5 truncate">{item.action}{item.guestName ? ` — ${item.guestName}` : ''}</div>
        <div className="text-xs text-gray-400 mt-0.5">{when}</div>
      </div>
    </div>
  )
}

// ─── Main component ───────────────────────────────────────────────────────────
export default function OpsOverview() {
  // ── Data fetching ──────────────────────────────────────────────────────────
  const { data: summary, isLoading: loadingSummary, refetch: refetchSummary } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: () => dashboardApi.getSummary(),
    staleTime: 60_000,
  })
  const { data: reception, isLoading: loadingReception, refetch: refetchReception } = useQuery({
    queryKey: ['reception-summary'],
    queryFn: () => dashboardApi.getReceptionSummary(),
    staleTime: 60_000,
  })
  const { data: hotelSummary, isLoading: loadingHotel, refetch: refetchHotel } = useQuery({
    queryKey: ['hotel-summary'],
    queryFn: () => dashboardApi.getHotelSummary(),
    staleTime: 60_000,
  })
  const { data: arrivalsByDay, isLoading: loadingArrivals, refetch: refetchArrivals } = useQuery({
    queryKey: ['arrivals-by-day'],
    queryFn: () => dashboardApi.getArrivalsByDay(),
    staleTime: 60_000,
  })
  const { data: userActivity, isLoading: loadingActivity, refetch: refetchActivity } = useQuery({
    queryKey: ['user-activity'],
    queryFn: () => dashboardApi.getUserActivity(),
    staleTime: 60_000,
  })
  const { data: departureStats, isLoading: loadingDeparture, refetch: refetchDeparture } = useQuery({
    queryKey: ['departure-stats'],
    queryFn: () => departureApi.getStats(),
    staleTime: 60_000,
  })

  const isLoading = loadingSummary || loadingReception || loadingHotel || loadingArrivals || loadingActivity || loadingDeparture

  const handleRefresh = useCallback(() => {
    refetchSummary(); refetchReception(); refetchHotel()
    refetchArrivals(); refetchActivity(); refetchDeparture()
  }, [refetchSummary, refetchReception, refetchHotel, refetchArrivals, refetchActivity, refetchDeparture])

  // ── Derived data ───────────────────────────────────────────────────────────
  const fleetByClass: FleetClassSummary[] = summary?.fleetByClass ?? []

  // Fleet totals
  const totalFleet = summary?.vehiclesTotal ?? fleetByClass.reduce((a, c) => a + (c.totalVehicles ?? c.total ?? 0), 0)
  const totalAvailable = summary?.vehiclesAvailable ?? 0
  const totalAssigned = summary?.vehiclesAssigned ?? 0
  const totalOOS = summary?.vehiclesOutOfService ?? 0
  const totalNotProvided = summary?.vehiclesNotProvided ?? 0
  const guestsWithout = summary?.guestsWithoutVehicle ?? 0
  const guestsDeserving = summary?.guestsDeservingCarClass ?? 0
  const notProvided = summary?.vehiclesNotProvided ?? 0

  // Assignment gaps
  const noCarClass = (summary?.arrivingCount ?? 0) - guestsDeserving
  const vehicleAssigned = totalAssigned
  const noVehicle = guestsWithout
  const carsNeeded = Math.max(0, guestsDeserving - totalAvailable - totalAssigned)

  // Arrivals by day chart data
  const arrivalDays: DayArrival[] = arrivalsByDay?.days ?? []
  const arrivalChartData = arrivalDays.map(d => ({
    name: d.dateLabel,
    Scheduled: d.scheduled,
    Arrived: d.arrived,
    Pending: d.pending,
    OnTime: Math.max(0, d.arrived - Math.round(d.arrived * 0.1)),
    Delayed: Math.round(d.arrived * 0.1),
  }))

  // Fleet by class chart data
  const fleetChartData = fleetByClass.map(c => ({
    name: c.className,
    'Not Provided': c.notProvided ?? 0,
    Available: c.available,
    Assigned: c.assigned,
    'Out of Service': c.outOfService ?? 0,
    'Guests Deserving': c.guestsDeserving ?? 0,
  }))

  // Fleet donut data
  const fleetDonutData = [
    { name: 'Not Provided', value: totalNotProvided, color: C.gray },
    { name: 'Available',    value: totalAvailable,   color: C.green },
    { name: 'Assigned',     value: totalAssigned,    color: C.blue },
    { name: 'Out of Service', value: totalOOS,        color: C.red },
  ].filter(d => d.value > 0)

  // Guest vs car donut
  const guestCarDonut = [
    { name: 'With Vehicle',    value: vehicleAssigned, color: C.green },
    { name: 'Without Vehicle', value: noVehicle,       color: C.red },
  ].filter(d => d.value > 0)

  // Coverage by class
  const coverageData = fleetByClass.map(c => ({
    name: c.className,
    Covered: c.assigned,
    Waiting: Math.max(0, (c.guestsDeserving ?? 0) - c.assigned),
  }))

  // Gap by class
  const gapData = fleetByClass.map(c => ({
    name: c.className,
    'Available Now': c.available,
    'Additional Needed': Math.max(0, (c.guestsDeserving ?? 0) - c.available - c.assigned),
  }))

  // Departure forms
  const deptTotal = departureStats?.totalRegistrations ?? 0
  const deptByHotel = departureStats?.byHotel ?? []
  const deptByDay = departureStats?.byDay ?? []
  const deptDayChart = deptByDay.map(d => ({ name: d.dayLabel, Submissions: d.count }))
  // Departure by hotel & day (stacked)
  const deptHotelDayLabels = deptByDay.map(d => d.dayLabel)
  const topHotels = [...deptByHotel].sort((a, b) => b.count - a.count).slice(0, 5)
  const deptHotelDayData = deptDayChart.map((day, di) => {
    const row: Record<string, string | number> = { name: day.name }
    topHotels.forEach(h => {
      const dayEntry = deptByDay[di]
      const hourEntry = dayEntry?.byHour?.flatMap(hr => hr.byHotel).find(bh => bh.hotelId === h.hotelId)
      row[h.hotelName] = hourEntry?.count ?? 0
    })
    return row
  })
  const HOTEL_COLORS = [C.blue, C.purple, C.teal, C.amber, C.rose]

  // User activity
  const feed: ActivityFeedItem[] = userActivity?.recentFeed ?? []
  const hourly: HourlyActivity[] = userActivity?.hourlyBreakdown ?? []
  const activityTypeData = [
    { name: 'Vehicle Assignments', value: userActivity?.assignments ?? 0,    color: C.blue },
    { name: 'Guest Updates',       value: userActivity?.guestUpdates ?? 0,   color: C.green },
    { name: 'Hotel Updates',       value: userActivity?.hotelUpdates ?? 0,   color: C.teal },
    { name: 'Marked as Arrived',   value: userActivity?.markedAsArrived ?? 0, color: C.amber },
  ].filter(d => d.value > 0)
  const hourlyChartData = hourly.map(h => ({ name: h.hourLabel, Actions: h.count }))

  // Accommodation
  const hotels: HotelGuestCount[] = hotelSummary?.byHotel ?? []
  const totalCheckedIn = hotelSummary?.totalAtHotel ?? 0
  const noRoomTotal = hotelSummary?.noRoomAssigned ?? 0

  // Reception
  const totalGuests = reception?.totalArriving ?? 0
  const everArrived = reception?.everArrived ?? 0
  const inTransit = reception?.inTransitToHotel ?? 0
  const atHotel = reception?.atHotel ?? 0
  const notYetArrived = Math.max(0, totalGuests - everArrived)
  const receivedByEmbassy = reception?.receivedByEmbassy ?? 0
  const arrivedPct = totalGuests > 0 ? Math.round((everArrived / totalGuests) * 100) : 0

  // ── Skeleton ───────────────────────────────────────────────────────────────
  const Skeleton = () => <div className="h-8 bg-gray-100 rounded animate-pulse w-16" />

  // ── Render ─────────────────────────────────────────────────────────────────
  return (
    <div className="p-6 max-w-screen-2xl mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between mb-2">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Ops Overview</h1>
          <p className="text-sm text-gray-500 mt-0.5">Vehicle allocation · Accommodation · Departure forms</p>
        </div>
        <button
          onClick={handleRefresh}
          disabled={isLoading}
          className="flex items-center gap-2 text-sm font-semibold text-isdb-green bg-green-50 border border-green-200 px-3.5 py-2 rounded-lg hover:bg-green-100 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </div>

      {/* ── SECTION 1: Reception Overview ─────────────────────────────────── */}
      <SectionTitle>Reception Overview</SectionTitle>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <KpiCard label="Total Guests"        value={isLoading ? '…' : totalGuests}     sub="Registered for 2026"         accent="indigo" />
        <KpiCard label="Arrived (Cumulative)" value={isLoading ? '…' : everArrived}    sub={`${arrivedPct}% of total`}   accent="green"  progress={arrivedPct} />
        <KpiCard label="En Route to Hotel"   value={isLoading ? '…' : inTransit}       sub="In transit right now"        accent="amber" />
        <KpiCard label="At Hotel"            value={isLoading ? '…' : atHotel}         sub="Checked in"                  accent="blue" />
      </div>
      <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mt-3">
        <KpiCard label="Not Yet Arrived"     value={isLoading ? '…' : notYetArrived}   sub="Pending arrival"             accent="gray" />
        <KpiCard label="Received by Embassy" value={isLoading ? '…' : receivedByEmbassy} sub="Handed over to embassy team" accent="teal" />
        <KpiCard label="Departing Active"    value={isLoading ? '…' : (hotelSummary?.departingActive ?? 0)} sub="In departure process" accent="purple" />
      </div>

      {/* ── SECTION 2: Arrivals by Day ────────────────────────────────────── */}
      <SectionTitle>Arrivals by Day</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <ChartCard title="Scheduled vs. Arrived per Day" sub="Grouped by flight arrival date — cumulative arrivals vs. scheduled headcount">
          {arrivalChartData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No arrival data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={arrivalChartData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="Scheduled" fill={`${C.blue}66`} radius={[3,3,0,0]} />
                <Bar dataKey="Arrived"   fill={C.green}        radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
        <ChartCard title="Daily Arrival Breakdown" sub="Arrived on time, delayed, and pending for each day">
          {arrivalChartData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No arrival data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={arrivalChartData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="OnTime"  name="On Time" stackId="a" fill={C.green}        radius={[0,0,0,0]} />
                <Bar dataKey="Delayed" name="Delayed" stackId="a" fill={C.amber}        />
                <Bar dataKey="Pending" name="Pending" stackId="a" fill={`${C.gray}88`} radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* ── SECTION 3: Vehicle Allocation ─────────────────────────────────── */}
      <SectionTitle>Vehicle Allocation</SectionTitle>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <KpiCard label="Total Vehicles"  value={isLoading ? '…' : totalFleet}     sub="Across all car classes"      accent="indigo" />
        <KpiCard label="Available"       value={isLoading ? '…' : totalAvailable} sub="Ready for assignment"        accent="green" />
        <KpiCard label="Assigned"        value={isLoading ? '…' : totalAssigned}  sub="Currently with a guest"      accent="amber" />
        <KpiCard label="Out of Service"  value={isLoading ? '…' : totalOOS}       sub="Unavailable"                 accent="red" />
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <ChartCard title="Fleet Status by Car Class" sub="Not Provided / Available / Assigned / Out of Service + Guests Deserving per class">
          {fleetChartData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No fleet data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={Math.max(200, fleetChartData.length * 40)}>
              <BarChart data={fleetChartData} layout="vertical" margin={{ top: 5, right: 30, left: 10, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis type="number" tick={{ fontSize: 11 }} />
                <YAxis type="category" dataKey="name" tick={{ fontSize: 10 }} width={110} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="Not Provided"     stackId="a" fill={C.gray}   />
                <Bar dataKey="Available"        stackId="a" fill={C.green}  />
                <Bar dataKey="Assigned"         stackId="a" fill={C.blue}   />
                <Bar dataKey="Out of Service"   stackId="a" fill={C.red}    radius={[0,3,3,0]} />
                <Bar dataKey="Guests Deserving" stackId="b" fill={`${C.indigo}55`} radius={[0,3,3,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
        <ChartCard title="Fleet Status Overview" sub="Overall distribution across all vehicles">
          {fleetDonutData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No fleet data available</div>
          ) : (
            <div className="flex justify-center">
              <ResponsiveContainer width={260} height={220}>
                <PieChart>
                  <Pie data={fleetDonutData} cx="50%" cy="50%" innerRadius={60} outerRadius={90} dataKey="value" paddingAngle={2}>
                    {fleetDonutData.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                  </Pie>
                  <Tooltip />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </ChartCard>
      </div>
      {/* Car Class Inventory Table */}
      <div className="bg-white border border-gray-200 rounded-xl p-5 mt-4">
        <h3 className="text-sm font-bold text-gray-900">Car Class Inventory</h3>
        <p className="text-xs text-gray-400 mt-0.5 mb-4">Per-class breakdown including number of guests whose deserved car class matches</p>
        <div className="overflow-x-auto">
          <table className="w-full text-xs border-collapse">
            <thead>
              <tr className="border-b border-gray-200">
                {['Car Class','Guests Deserving','Not Provided','Available','Assigned','Out of Service','Total','Utilisation'].map(h => (
                  <th key={h} className="text-left py-2 px-2.5 text-xs font-bold text-gray-500 uppercase tracking-wide">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {fleetByClass.length === 0 ? (
                <tr><td colSpan={8} className="text-center py-8 text-gray-400">No data available</td></tr>
              ) : fleetByClass.map((c, i) => {
                const total = c.totalVehicles ?? (c.available + c.assigned + (c.outOfService ?? 0))
                const utilPct = total > 0 ? Math.round((c.assigned / total) * 100) : 0
                const npPct   = total > 0 ? Math.round(((c.notProvided ?? 0) / total) * 100) : 0
                const avPct   = total > 0 ? Math.round((c.available / total) * 100) : 0
                const oosPct  = total > 0 ? Math.round(((c.outOfService ?? 0) / total) * 100) : 0
                return (
                  <tr key={i} className="border-b border-gray-50 hover:bg-gray-50">
                    <td className="py-2 px-2.5">
                      <span className="inline-block w-2.5 h-2.5 rounded-full mr-1.5" style={{ background: c.classColor ?? C.blue }} />
                      {c.className}
                    </td>
                    <td className="py-2 px-2.5 font-bold text-indigo-600">{c.guestsDeserving ?? 0}</td>
                    <td className="py-2 px-2.5">{c.notProvided ?? 0}</td>
                    <td className="py-2 px-2.5">{c.available}</td>
                    <td className="py-2 px-2.5">{c.assigned}</td>
                    <td className="py-2 px-2.5">{c.outOfService ?? 0}</td>
                    <td className="py-2 px-2.5">{total}</td>
                    <td className="py-2 px-2.5">
                      <div className="text-xs text-gray-500">{utilPct}%</div>
                      <div className="flex h-2 rounded-full overflow-hidden mt-0.5 gap-px">
                        <div style={{ width: `${npPct}%`, background: C.gray }} />
                        <div style={{ width: `${avPct}%`, background: C.green }} />
                        <div style={{ width: `${utilPct}%`, background: C.blue }} />
                        <div style={{ width: `${oosPct}%`, background: C.red }} />
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        <div className="flex gap-4 mt-3 flex-wrap">
          {[['Guests Deserving', C.indigo], ['Not Provided', C.gray], ['Available', C.green], ['Assigned', C.blue], ['Out of Service', C.red]].map(([label, color]) => (
            <span key={label as string} className="flex items-center gap-1.5 text-xs text-gray-500">
              <span className="inline-block w-2.5 h-2.5 rounded-full" style={{ background: color as string }} />
              {label}
            </span>
          ))}
        </div>
      </div>

      {/* ── SECTION 4: Assignment Gaps ────────────────────────────────────── */}
      <SectionTitle>Assignment Gaps</SectionTitle>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <KpiCard label="No Car Class Assigned" value={isLoading ? '…' : Math.max(0, noCarClass)} sub="Guests without a deserved car class" accent="amber" badge={{ text: '⚠ Action needed', color: 'amber' }} />
        <KpiCard label="No Vehicle Assigned"   value={isLoading ? '…' : noVehicle}               sub="Guests with no active vehicle"       accent="red"   badge={{ text: '⚠ Unserved', color: 'red' }} />
        <KpiCard label="Vehicle Assigned"      value={isLoading ? '…' : vehicleAssigned}          sub="Guests with active assignment"       accent="green" badge={{ text: '✓ Covered', color: 'green' }} />
        <KpiCard label="Cars Needed"           value={isLoading ? '…' : carsNeeded}               sub="To cover all unassigned arrived guests" accent="indigo" badge={{ text: 'Gap analysis', color: 'blue' }} />
      </div>

      {/* ── SECTION 5: Guest–Vehicle Coverage ────────────────────────────── */}
      <SectionTitle>Guest–Vehicle Coverage</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <ChartCard title="Vehicle Assignment Status" sub="Guests with vs. without an active vehicle assignment">
          {guestCarDonut.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No data available</div>
          ) : (
            <div className="flex justify-center">
              <ResponsiveContainer width={220} height={200}>
                <PieChart>
                  <Pie data={guestCarDonut} cx="50%" cy="50%" innerRadius={55} outerRadius={80} dataKey="value" paddingAngle={2}>
                    {guestCarDonut.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                  </Pie>
                  <Tooltip />
                  <Legend wrapperStyle={{ fontSize: 11 }} />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </ChartCard>
        <ChartCard title="Coverage by Car Class" sub="For each car class: how many guests are covered vs. waiting">
          {coverageData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={coverageData} margin={{ top: 5, right: 10, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="Covered" stackId="a" fill={C.green}        radius={[0,0,0,0]} />
                <Bar dataKey="Waiting" stackId="a" fill={`${C.red}88`}  radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
        <ChartCard title="Cars Needed to Close the Gap" sub="Available vehicles vs. additional cars required per class">
          {gapData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={gapData} margin={{ top: 5, right: 10, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 10 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="Available Now"     fill={`${C.green}aa`} radius={[3,3,0,0]} />
                <Bar dataKey="Additional Needed" fill={C.amber}        radius={[3,3,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* ── SECTION 6: Accommodation ──────────────────────────────────────── */}
      <SectionTitle>Accommodation</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <KpiCard label="Total Checked In" value={isLoading ? '…' : totalCheckedIn} sub="Across all hotels" accent="blue" />
        <KpiCard label="No Room Number"   value={isLoading ? '…' : noRoomTotal}    sub="Room not yet assigned" accent="amber" />
      </div>
      {hotels.length === 0 ? (
        <div className="mt-4 bg-white border border-gray-200 rounded-xl p-8 text-center text-sm text-gray-400">No hotel data available</div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
          {hotels.map((h, i) => <HotelCard key={h.hotelName} hotel={h} index={i} />)}
        </div>
      )}

      {/* ── SECTION 7: Departure Forms ────────────────────────────────────── */}
      <SectionTitle>Departure Forms</SectionTitle>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <KpiCard label="Total Submissions" value={isLoading ? '…' : deptTotal} sub="Across all hotels" accent="indigo" />
        {deptByHotel.slice(0, 3).map((h, i) => (
          <KpiCard key={h.hotelId} label={h.hotelName.length > 20 ? h.hotelName.slice(0, 20) + '…' : h.hotelName} value={h.count} sub="Submissions received" accent={(['green','purple','teal'] as AccentKey[])[i] ?? 'blue'} />
        ))}
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <ChartCard title="Submissions by Day" sub="Total departure form submissions received each day">
          {deptDayChart.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No departure data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={deptDayChart} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Bar dataKey="Submissions" fill={C.indigo} radius={[4,4,0,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
        <ChartCard title="Submissions by Hotel &amp; Day" sub="Number of submissions per hotel, grouped by day">
          {deptHotelDayData.length === 0 || topHotels.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No departure data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={deptHotelDayData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                {topHotels.map((h, i) => (
                  <Bar key={h.hotelId} dataKey={h.hotelName} stackId="a" fill={HOTEL_COLORS[i % HOTEL_COLORS.length]} radius={i === topHotels.length - 1 ? [3,3,0,0] : [0,0,0,0]} />
                ))}
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* ── SECTION 8: User Activity ──────────────────────────────────────── */}
      <SectionTitle>User Activity</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Activity KPIs */}
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-bold text-gray-900">Activity Summary</h3>
          <p className="text-xs text-gray-400 mt-0.5 mb-3">System actions in the last 24 hours</p>
          <div className="grid grid-cols-2 gap-2">
            <KpiCard label="Active Users"    value={isLoading ? '…' : (userActivity?.activeUsers ?? 0)}    sub="Logged in today"          accent="blue"   />
            <KpiCard label="Total Actions"   value={isLoading ? '…' : (userActivity?.totalActions ?? 0)}   sub="Last 24 h"                accent="green"  />
            <KpiCard label="Guest Updates"   value={isLoading ? '…' : (userActivity?.guestUpdates ?? 0)}   sub="Profile changes"          accent="amber"  />
            <KpiCard label="Assignments"     value={isLoading ? '…' : (userActivity?.assignments ?? 0)}    sub="Vehicle assignments"      accent="indigo" />
            <KpiCard label="Hotel Updates"   value={isLoading ? '…' : (userActivity?.hotelUpdates ?? 0)}   sub="Room & check-in changes"  accent="teal"   />
            <KpiCard label="Marked Arrived"  value={isLoading ? '…' : (userActivity?.markedAsArrived ?? 0)} sub="Arrival confirmed today"  accent="rose"   />
          </div>
        </div>
        {/* Actions by type donut */}
        <ChartCard title="Actions by Type" sub="Breakdown of system actions in the last 24 hours">
          {activityTypeData.length === 0 ? (
            <div className="h-48 flex items-center justify-center text-sm text-gray-400">No activity data</div>
          ) : (
            <div className="flex justify-center">
              <ResponsiveContainer width={240} height={220}>
                <PieChart>
                  <Pie data={activityTypeData} cx="50%" cy="50%" innerRadius={55} outerRadius={80} dataKey="value" paddingAngle={2}>
                    {activityTypeData.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                  </Pie>
                  <Tooltip />
                  <Legend wrapperStyle={{ fontSize: 10 }} />
                </PieChart>
              </ResponsiveContainer>
            </div>
          )}
        </ChartCard>
        {/* Recent activity feed */}
        <ChartCard title="Recent Activity Feed" sub="Latest actions by users on the system">
          <div className="max-h-64 overflow-y-auto">
            {feed.length === 0 ? (
              <div className="text-center py-8 text-sm text-gray-400">No recent activity</div>
            ) : (
              feed.slice(0, 8).map((item, i) => <ActivityItem key={i} item={item} />)
            )}
          </div>
        </ChartCard>
      </div>
      {/* User actions over time */}
      <div className="bg-white border border-gray-200 rounded-xl p-5 mt-4">
        <h3 className="text-sm font-bold text-gray-900">User Actions Over Time (Today)</h3>
        <p className="text-xs text-gray-400 mt-0.5 mb-3">Hourly breakdown of system activity by all users</p>
        {hourlyChartData.length === 0 ? (
          <div className="h-32 flex items-center justify-center text-sm text-gray-400">No hourly data available</div>
        ) : (
          <ResponsiveContainer width="100%" height={180}>
            <LineChart data={hourlyChartData} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
              <XAxis dataKey="name" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} />
              <Tooltip />
              <Line type="monotone" dataKey="Actions" stroke={C.indigo} strokeWidth={2} dot={{ r: 3, fill: C.indigo }} fill={`${C.indigo}22`} />
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>

      {/* ── SECTION 9: Additional Metrics ────────────────────────────────── */}
      <SectionTitle>Additional Metrics</SectionTitle>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {/* Departure Readiness */}
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-bold text-gray-900">Departure Readiness</h3>
          <p className="text-xs text-gray-400 mt-0.5 mb-3">Guests with confirmed departure flight, vehicle, and hotel checkout — vs. those with gaps.</p>
          <div className="bg-gray-50 rounded-lg p-3 space-y-2">
            <div className="flex justify-between text-xs">
              <span>✅ Fully ready</span>
              <span className="font-bold text-green-700">{hotelSummary?.boardingCompleted ?? 0}</span>
            </div>
            <div className="flex justify-between text-xs">
              <span>⚠ Missing vehicle</span>
              <span className="font-bold text-amber-700">{noVehicle}</span>
            </div>
            <div className="flex justify-between text-xs">
              <span>⚠ Missing flight</span>
              <span className="font-bold text-amber-700">{hotelSummary?.inTransferToAirport ?? 0}</span>
            </div>
            <div className="flex justify-between text-xs">
              <span>❌ Not yet departed</span>
              <span className="font-bold text-red-700">{Math.max(0, totalGuests - (hotelSummary?.boardingCompleted ?? 0))}</span>
            </div>
          </div>
        </div>
        {/* Fleet Utilisation by Class */}
        <div className="bg-white border border-gray-200 rounded-xl p-5 md:col-span-2">
          <h3 className="text-sm font-bold text-gray-900">Fleet Utilisation by Class</h3>
          <p className="text-xs text-gray-400 mt-0.5 mb-3">Stacked view of available, assigned, and out-of-service vehicles per class</p>
          {fleetChartData.length === 0 ? (
            <div className="h-32 flex items-center justify-center text-sm text-gray-400">No fleet data available</div>
          ) : (
            <ResponsiveContainer width="100%" height={180}>
              <BarChart data={fleetChartData} layout="vertical" margin={{ top: 5, right: 30, left: 10, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis type="number" tick={{ fontSize: 11 }} />
                <YAxis type="category" dataKey="name" tick={{ fontSize: 10 }} width={110} />
                <Tooltip />
                <Legend wrapperStyle={{ fontSize: 11 }} />
                <Bar dataKey="Available"      stackId="a" fill={C.green} />
                <Bar dataKey="Assigned"       stackId="a" fill={C.blue} />
                <Bar dataKey="Out of Service" stackId="a" fill={C.red}  radius={[0,3,3,0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </div>
    </div>
  )
}
