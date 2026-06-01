// ExportRosterPage.tsx
// Admin-only page for exporting the guest roster as a filtered CSV.
// Bundle variable: ExportRosterPage (injected as function ExportRosterPage(){...})
// Bundle aliases used:
//   N  = React
//   s  = jsx runtime
//   ae = useQuery (react-query)
//   qt = useNavigate
//   Oe = guestsApi  (GET /api/guests)
//   _  = axios instance
//   fa = Download icon
//   Pb = Filter icon
//   vt = Users icon
//   As = Check icon
//   Za = SquareCheckBig icon
//   f0 = Square icon
//   yt = RefreshCw icon
//   Fe = X icon
//   Sv = downloadBlob(url, filename, params) helper

import React, { useState, useMemo } from 'react'
import { Download, Filter, Users, SquareCheckBig, Square, RefreshCw, X } from 'lucide-react'

// Sentinel value sent to backend to represent null/empty rank or car class
const UNSET = '__UNSET__'
const UNSET_LABEL = '(Not Set)'

// ─── Column definitions ───────────────────────────────────────────────────────
const ALL_COLUMNS = [
  { key: 'title',             label: 'Title' },
  { key: 'name',              label: 'Name' },
  { key: 'rank',              label: 'Rank' },
  { key: 'country',           label: 'Country' },
  { key: 'registrationType',  label: 'Registration Type' },
  { key: 'deservedCarClass',  label: 'Deserve Car Class' },
  { key: 'arrivalFlight',     label: 'Arrival Flight No.' },
  { key: 'arrivalAirline',    label: 'Arrival Airline' },
  { key: 'arrivalDateTime',   label: 'Arrival Date/Time' },
  { key: 'arrivalRoute',      label: 'Arrival Route' },
  { key: 'departureFlight',   label: 'Departure Flight No.' },
  { key: 'departureAirline',  label: 'Departure Airline' },
  { key: 'departureDatetime', label: 'Departure Date/Time' },
  { key: 'departureRoute',    label: 'Departure Route' },
  { key: 'carNumber',         label: 'Car Number' },
  { key: 'driverName',        label: 'Driver Name' },
  { key: 'driverPhone',       label: 'Driver Phone' },
  { key: 'hotelName',         label: 'Hotel Name' },
  { key: 'roomNumber',        label: 'Room Number' },
]

// ─── Multi-select checkbox group component ────────────────────────────────────
// options: display labels (strings)
// selected: Set of values (may differ from labels when useUnset=true)
// getVal: maps label → value sent to backend (default: identity)
function CheckboxGroup({
  label,
  options,
  selected,
  onToggle,
  onSelectAll,
  onClearAll,
  unsetCount,
}: {
  label: string
  options: string[]
  selected: Set<string>
  onToggle: (v: string) => void
  onSelectAll: () => void
  onClearAll: () => void
  unsetCount?: number   // if provided, show (Not Set) option
}) {
  // All options including the synthetic UNSET
  const allOpts = unsetCount !== undefined ? [UNSET, ...options] : options
  const allSelected = allOpts.length > 0 && allOpts.every(o => selected.has(o))

  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center justify-between mb-3">
        <span className="text-sm font-semibold text-gray-700">{label}</span>
        <div className="flex gap-2">
          <button
            onClick={onSelectAll}
            className={`text-xs px-2 py-1 rounded-md font-medium transition-colors ${
              allSelected
                ? 'bg-isdb-green text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-isdb-green/10 hover:text-isdb-green'
            }`}
          >
            Select All
          </button>
          <button
            onClick={onClearAll}
            className="text-xs px-2 py-1 rounded-md font-medium bg-gray-100 text-gray-600 hover:bg-red-50 hover:text-red-600 transition-colors"
          >
            Clear All
          </button>
        </div>
      </div>
      {allOpts.length === 0 ? (
        <p className="text-xs text-gray-400 italic">No options available</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {/* (Not Set) option — rendered first with a distinct style */}
          {unsetCount !== undefined && (
            <button
              key={UNSET}
              onClick={() => onToggle(UNSET)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                selected.has(UNSET)
                  ? 'bg-amber-50 border-amber-400 text-amber-700'
                  : 'bg-white border-dashed border-gray-300 text-gray-400 hover:border-amber-400 hover:text-amber-600'
              }`}
            >
              {selected.has(UNSET)
                ? <SquareCheckBig className="w-3.5 h-3.5" />
                : <Square className="w-3.5 h-3.5" />}
              {UNSET_LABEL}
              {unsetCount > 0 && (
                <span className="ml-1 text-[10px] opacity-70">({unsetCount})</span>
              )}
            </button>
          )}
          {/* Real value options */}
          {options.map(opt => {
            const checked = selected.has(opt)
            return (
              <button
                key={opt}
                onClick={() => onToggle(opt)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                  checked
                    ? 'bg-isdb-green/10 border-isdb-green text-isdb-green'
                    : 'bg-white border-gray-200 text-gray-600 hover:border-isdb-green/50 hover:text-isdb-green'
                }`}
              >
                {checked ? <SquareCheckBig className="w-3.5 h-3.5" /> : <Square className="w-3.5 h-3.5" />}
                {opt}
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

// ─── Column selector ──────────────────────────────────────────────────────────
function ColumnSelector({
  selectedCols,
  onToggle,
  onSelectAll,
  onClearAll,
}: {
  selectedCols: Set<string>
  onToggle: (k: string) => void
  onSelectAll: () => void
  onClearAll: () => void
}) {
  const allSelected = ALL_COLUMNS.every(c => selectedCols.has(c.key))
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4">
      <div className="flex items-center justify-between mb-3">
        <span className="text-sm font-semibold text-gray-700">Columns to Export</span>
        <div className="flex gap-2">
          <button
            onClick={onSelectAll}
            className={`text-xs px-2 py-1 rounded-md font-medium transition-colors ${
              allSelected
                ? 'bg-isdb-green text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-isdb-green/10 hover:text-isdb-green'
            }`}
          >
            Select All
          </button>
          <button
            onClick={onClearAll}
            className="text-xs px-2 py-1 rounded-md font-medium bg-gray-100 text-gray-600 hover:bg-red-50 hover:text-red-600 transition-colors"
          >
            Clear All
          </button>
        </div>
      </div>
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
        {ALL_COLUMNS.map(col => {
          const checked = selectedCols.has(col.key)
          return (
            <button
              key={col.key}
              onClick={() => onToggle(col.key)}
              className={`flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium border transition-colors text-left ${
                checked
                  ? 'bg-isdb-green/10 border-isdb-green text-isdb-green'
                  : 'bg-white border-gray-200 text-gray-600 hover:border-isdb-green/50 hover:text-isdb-green'
              }`}
            >
              {checked ? <SquareCheckBig className="w-3.5 h-3.5 flex-shrink-0" /> : <Square className="w-3.5 h-3.5 flex-shrink-0" />}
              {col.label}
            </button>
          )
        })}
      </div>
    </div>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────
export default function ExportRosterPage() {
  const { data: guests = [], isLoading, refetch } = useQuery({
    queryKey: ['guests', 'all'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 60_000,
  })

  // Derive filter options from loaded guest data
  const regTypeOptions = useMemo(() => {
    const s = new Set(guests.map((g: any) => g.registrationTypeName).filter(Boolean))
    return Array.from(s).sort() as string[]
  }, [guests])

  const rankOptions = useMemo(() => {
    const s = new Set(guests.map((g: any) => g.rankValue).filter(Boolean))
    return Array.from(s).sort() as string[]
  }, [guests])

  // Count guests with no rank
  const unsetRankCount = useMemo(() =>
    guests.filter((g: any) => !g.rankValue).length, [guests])

  const carClassOptions = useMemo(() => {
    const map = new Map<string, string>()
    guests.forEach((g: any) => {
      if (g.deservedCarClassId && g.deservedCarClassName)
        map.set(g.deservedCarClassId, g.deservedCarClassName)
    })
    return Array.from(map.entries())
      .sort((a, b) => a[1].localeCompare(b[1]))
      .map(([id, name]) => ({ id, name }))
  }, [guests])

  // Count guests with no car class
  const unsetCarClassCount = useMemo(() =>
    guests.filter((g: any) => !g.deservedCarClassId).length, [guests])

  // Filter state — empty Set means "all selected" (no filter applied)
  const [selRegTypes, setSelRegTypes] = useState<Set<string>>(new Set())
  // Rank: values are actual rankValue strings OR UNSET sentinel
  const [selRanks, setSelRanks] = useState<Set<string>>(new Set())
  // Car class: values are carClassId strings OR UNSET sentinel
  const [selCarClassIds, setSelCarClassIds] = useState<Set<string>>(new Set())
  const [selCols, setSelCols] = useState<Set<string>>(new Set(ALL_COLUMNS.map(c => c.key)))
  const [exporting, setExporting] = useState(false)

  // Preview count — client-side filtering (mirrors backend logic)
  const previewCount = useMemo(() => {
    return guests.filter((g: any) => {
      if (selRegTypes.size > 0 && !selRegTypes.has(g.registrationTypeName)) return false
      if (selRanks.size > 0) {
        const hasUnset = selRanks.has(UNSET)
        const realRanks = Array.from(selRanks).filter(r => r !== UNSET)
        const guestRank = g.rankValue
        const matchUnset = hasUnset && !guestRank
        const matchReal = realRanks.length > 0 && guestRank && realRanks.includes(guestRank)
        if (!matchUnset && !matchReal) return false
      }
      if (selCarClassIds.size > 0) {
        const hasUnset = selCarClassIds.has(UNSET)
        const realIds = Array.from(selCarClassIds).filter(r => r !== UNSET)
        const guestClassId = g.deservedCarClassId
        const matchUnset = hasUnset && !guestClassId
        const matchReal = realIds.length > 0 && guestClassId && realIds.includes(guestClassId)
        if (!matchUnset && !matchReal) return false
      }
      return true
    }).length
  }, [guests, selRegTypes, selRanks, selCarClassIds])

  // Toggle helpers
  const toggle = (set: Set<string>, val: string): Set<string> => {
    const next = new Set(set)
    next.has(val) ? next.delete(val) : next.add(val)
    return next
  }

  // Export handler
  const handleExport = async () => {
    if (selCols.size === 0) return
    setExporting(true)
    try {
      const params: Record<string, string> = {}
      if (selRegTypes.size > 0) params.registrationTypes = Array.from(selRegTypes).join(',')
      if (selRanks.size > 0) params.ranks = Array.from(selRanks).join(',')
      if (selCarClassIds.size > 0) params.deservedCarClassIds = Array.from(selCarClassIds).join(',')
      params.columns = Array.from(selCols).join(',')

      const date = new Date().toISOString().slice(0, 10)
      await downloadBlob('/api/guests/export', `roster-${date}.csv`, params)
    } finally {
      setExporting(false)
    }
  }

  const hasFilters = selRegTypes.size > 0 || selRanks.size > 0 || selCarClassIds.size > 0

  // All-options lists including UNSET sentinel for rank and car class
  const allRankOpts = [UNSET, ...rankOptions]
  const allCarClassIds = [UNSET, ...carClassOptions.map(c => c.id)]

  return (
    <div className="p-4 md:p-6 max-w-5xl mx-auto space-y-5">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-isdb-green/10 flex items-center justify-center">
            <Download className="w-5 h-5 text-isdb-green" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Export Roster</h1>
            <p className="text-sm text-gray-500">Export guest roster data as a CSV file</p>
          </div>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center gap-2 px-3 py-2 text-sm text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </div>

      {/* Filters section */}
      <div className="space-y-3">
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-gray-500" />
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Filters</h2>
          {hasFilters && (
            <button
              onClick={() => { setSelRegTypes(new Set()); setSelRanks(new Set()); setSelCarClassIds(new Set()) }}
              className="ml-auto flex items-center gap-1 text-xs text-red-500 hover:text-red-700 transition-colors"
            >
              <X className="w-3 h-3" /> Clear all filters
            </button>
          )}
        </div>

        {/* Registration Type — no (Not Set) needed */}
        <CheckboxGroup
          label="Registration Type"
          options={regTypeOptions}
          selected={selRegTypes}
          onToggle={v => setSelRegTypes(toggle(selRegTypes, v))}
          onSelectAll={() => setSelRegTypes(new Set(regTypeOptions))}
          onClearAll={() => setSelRegTypes(new Set())}
        />

        {/* Rank — with (Not Set) */}
        <CheckboxGroup
          label="Rank"
          options={rankOptions}
          selected={selRanks}
          onToggle={v => setSelRanks(toggle(selRanks, v))}
          onSelectAll={() => setSelRanks(new Set(allRankOpts))}
          onClearAll={() => setSelRanks(new Set())}
          unsetCount={unsetRankCount}
        />

        {/* Deserve Car Class — with (Not Set) */}
        {/* Car class filter uses IDs internally but displays names */}
        <CheckboxGroup
          label="Deserve Car Class"
          options={carClassOptions.map(c => c.name)}
          selected={new Set([
            ...(selCarClassIds.has(UNSET) ? [UNSET] : []),
            ...Array.from(selCarClassIds)
              .filter(id => id !== UNSET)
              .map(id => carClassOptions.find(c => c.id === id)?.name ?? '')
              .filter(Boolean),
          ])}
          onToggle={name => {
            if (name === UNSET) {
              setSelCarClassIds(toggle(selCarClassIds, UNSET))
              return
            }
            const cls = carClassOptions.find(c => c.name === name)
            if (!cls) return
            setSelCarClassIds(toggle(selCarClassIds, cls.id))
          }}
          onSelectAll={() => setSelCarClassIds(new Set(allCarClassIds))}
          onClearAll={() => setSelCarClassIds(new Set())}
          unsetCount={unsetCarClassCount}
        />
      </div>

      {/* Column selector */}
      <ColumnSelector
        selectedCols={selCols}
        onToggle={k => setSelCols(toggle(selCols, k))}
        onSelectAll={() => setSelCols(new Set(ALL_COLUMNS.map(c => c.key)))}
        onClearAll={() => setSelCols(new Set())}
      />

      {/* Preview + Export */}
      <div className="bg-white rounded-xl border border-gray-200 p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div className="flex items-center gap-3">
          <Users className="w-5 h-5 text-gray-400" />
          <div>
            <p className="text-sm font-semibold text-gray-900">
              {isLoading ? 'Loading…' : `${previewCount} guest${previewCount !== 1 ? 's' : ''} match your filters`}
            </p>
            <p className="text-xs text-gray-500">
              {selCols.size === 0
                ? 'Select at least one column to export'
                : `${selCols.size} of ${ALL_COLUMNS.length} columns selected`}
            </p>
          </div>
        </div>
        <button
          onClick={handleExport}
          disabled={exporting || selCols.size === 0 || previewCount === 0}
          className="flex items-center gap-2 bg-isdb-green hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed text-white px-5 py-2.5 rounded-lg text-sm font-medium transition-colors"
        >
          <Download className="w-4 h-4" />
          {exporting ? 'Exporting…' : 'Export CSV'}
        </button>
      </div>
    </div>
  )
}
