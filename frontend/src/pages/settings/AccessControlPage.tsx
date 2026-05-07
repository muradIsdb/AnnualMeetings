import { useState, useEffect } from 'react'
import { Save, RotateCcw, Shield, Search, CheckCircle2, Info } from 'lucide-react'
import toast from 'react-hot-toast'
import { accessControlApi, type PagePermissionDto } from '../../api/services'

// ─── Page catalogue ───────────────────────────────────────────────────────────
interface PageDef {
  pageId: string
  label: string
  module: string
}

const PAGE_CATALOGUE: PageDef[] = [
  // Airport
  { pageId: 'airport.dashboard',          label: 'Airport Dashboard',        module: 'Airport' },
  // Transport
  { pageId: 'transport.dashboard',        label: 'Transport Dashboard',      module: 'Transport' },
  { pageId: 'transport.departure_stats',  label: 'Departure Shuttle',        module: 'Transport' },
  { pageId: 'fleet.management',           label: 'Fleet Management',         module: 'Transport' },
  // Control Room
  { pageId: 'controlroom.dashboard',      label: 'Control Room',             module: 'Control Room' },
  // Hotel
  { pageId: 'hotel.dashboard',            label: 'Hotel Dashboard',          module: 'Hotel' },
  { pageId: 'hotel.arrivals',             label: 'Arrivals Queue',           module: 'Hotel' },
  { pageId: 'hotel.guests',              label: 'Guest Directory',           module: 'Hotel' },
  { pageId: 'hotel.management',           label: 'Hotel Management',         module: 'Hotel' },
  // Liaison
  { pageId: 'liaison.dashboard',          label: 'Liaison Dashboard',        module: 'Liaison' },
  { pageId: 'liaison.guests',             label: 'Liaison Guest Directory',  module: 'Liaison' },
]

// ─── Role definitions ─────────────────────────────────────────────────────────
const ROLES = [
  { key: 'Airport',     label: 'Airport',       abbr: 'AP', color: 'text-blue-600' },
  { key: 'Transport',   label: 'Transport',      abbr: 'TR', color: 'text-orange-600' },
  { key: 'Hotel',       label: 'Hotel',          abbr: 'HT', color: 'text-purple-600' },
  { key: 'ControlRoom', label: 'Control Room',   abbr: 'CR', color: 'text-teal-600' },
  { key: 'Liaison',     label: 'Liaison',        abbr: 'LI', color: 'text-indigo-600' },
]

// ─── Helpers ──────────────────────────────────────────────────────────────────
type PermMatrix = Record<string, Record<string, boolean>> // role -> pageId -> granted

function buildMatrix(perms: PagePermissionDto[]): PermMatrix {
  const matrix: PermMatrix = {}
  for (const role of ROLES) {
    matrix[role.key] = {}
    for (const page of PAGE_CATALOGUE) {
      matrix[role.key][page.pageId] = false
    }
  }
  for (const p of perms) {
    if (matrix[p.role]) {
      matrix[p.role][p.pageId] = p.isGranted
    }
  }
  return matrix
}

function flattenMatrix(matrix: PermMatrix): PagePermissionDto[] {
  const result: PagePermissionDto[] = []
  for (const role of ROLES) {
    for (const page of PAGE_CATALOGUE) {
      result.push({ role: role.key, pageId: page.pageId, isGranted: matrix[role.key]?.[page.pageId] ?? false })
    }
  }
  return result
}

function matrixEqual(a: PermMatrix, b: PermMatrix): boolean {
  return JSON.stringify(a) === JSON.stringify(b)
}

// ─── Component ────────────────────────────────────────────────────────────────
export default function AccessControlPage() {
  const [matrix, setMatrix] = useState<PermMatrix>({})
  const [savedMatrix, setSavedMatrix] = useState<PermMatrix>({})
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')

  const hasUnsaved = !matrixEqual(matrix, savedMatrix)

  // ── Load permissions ─────────────────────────────────────────────────────────
  useEffect(() => {
    loadPermissions()
  }, [])

  async function loadPermissions() {
    setLoading(true)
    try {
      const perms = await accessControlApi.getAllPermissions()
      const m = buildMatrix(perms)
      setMatrix(m)
      setSavedMatrix(JSON.parse(JSON.stringify(m)))
    } catch {
      toast.error('Failed to load permissions')
    } finally {
      setLoading(false)
    }
  }

  // ── Toggle a single cell ──────────────────────────────────────────────────────
  function toggle(role: string, pageId: string) {
    setMatrix(prev => ({
      ...prev,
      [role]: { ...prev[role], [pageId]: !prev[role]?.[pageId] },
    }))
  }

  // ── Save ──────────────────────────────────────────────────────────────────────
  async function handleSave() {
    setSaving(true)
    try {
      await accessControlApi.updatePermissions(flattenMatrix(matrix))
      setSavedMatrix(JSON.parse(JSON.stringify(matrix)))
      toast.success('Permissions saved successfully')
    } catch {
      toast.error('Failed to save permissions')
    } finally {
      setSaving(false)
    }
  }

  // ── Discard ───────────────────────────────────────────────────────────────────
  function handleDiscard() {
    setMatrix(JSON.parse(JSON.stringify(savedMatrix)))
  }

  // ── Filtered pages ────────────────────────────────────────────────────────────
  const filteredPages = PAGE_CATALOGUE.filter(p =>
    p.label.toLowerCase().includes(search.toLowerCase()) ||
    p.module.toLowerCase().includes(search.toLowerCase()) ||
    p.pageId.toLowerCase().includes(search.toLowerCase())
  )

  // Group by module
  const modules = Array.from(new Set(filteredPages.map(p => p.module)))

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-isdb-green" />
      </div>
    )
  }

  return (
    <div className="p-6 max-w-7xl mx-auto">
      {/* ── Header ── */}
      <div className="flex items-start justify-between mb-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Shield className="w-5 h-5 text-isdb-green" />
            <h1 className="text-xl font-semibold text-gray-900">Access Control</h1>
            {hasUnsaved && (
              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-700">
                <span className="w-1.5 h-1.5 rounded-full bg-amber-500" />
                Unsaved changes
              </span>
            )}
          </div>
          <p className="text-sm text-gray-500">
            Define which pages each role can access. Changes take effect immediately after saving.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {hasUnsaved && (
            <button
              onClick={handleDiscard}
              className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
            >
              <RotateCcw className="w-4 h-4" />
              Discard Changes
            </button>
          )}
          <button
            onClick={handleSave}
            disabled={saving || !hasUnsaved}
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-white bg-isdb-green rounded-lg hover:bg-isdb-green-dark disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {saving ? (
              <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
            ) : (
              <Save className="w-4 h-4" />
            )}
            Save Permissions
          </button>
        </div>
      </div>

      {/* ── Admin notice ── */}
      <div className="flex items-start gap-2 p-3 mb-4 bg-blue-50 border border-blue-200 rounded-lg text-sm text-blue-700">
        <Info className="w-4 h-4 mt-0.5 flex-shrink-0" />
        <span>
          <strong>Admin</strong> always has access to all pages and cannot be restricted. The Admin column is not shown below.
        </span>
      </div>

      {/* ── Search ── */}
      <div className="relative mb-4 max-w-xs">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          type="text"
          placeholder="Filter pages…"
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-4 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-isdb-green focus:border-transparent"
        />
      </div>

      {/* ── Permission Matrix ── */}
      <div className="bg-white rounded-xl border border-gray-200 overflow-hidden shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-200">
                <th className="text-left px-4 py-3 font-medium text-gray-700 w-64">Page / Module</th>
                {ROLES.map(role => (
                  <th key={role.key} className="text-center px-3 py-3 font-medium min-w-[90px]">
                    <div className={`text-xs font-bold ${role.color}`}>{role.abbr}</div>
                    <div className="text-xs text-gray-500 font-normal">{role.label}</div>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {modules.map(module => {
                const pages = filteredPages.filter(p => p.module === module)
                return (
                  <>
                    {/* Module header row */}
                    <tr key={`module-${module}`} className="bg-gray-50/50">
                      <td colSpan={ROLES.length + 1} className="px-4 py-2">
                        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">{module} Module</span>
                      </td>
                    </tr>
                    {/* Page rows */}
                    {pages.map((page, idx) => (
                      <tr
                        key={page.pageId}
                        className={`border-t border-gray-100 hover:bg-gray-50/50 transition-colors ${idx % 2 === 0 ? '' : 'bg-gray-50/20'}`}
                      >
                        <td className="px-4 py-3">
                          <div className="font-medium text-gray-800">{page.label}</div>
                          <div className="text-xs text-gray-400 font-mono">{page.pageId}</div>
                        </td>
                        {ROLES.map(role => {
                          const granted = matrix[role.key]?.[page.pageId] ?? false
                          return (
                            <td key={role.key} className="text-center px-3 py-3">
                              <button
                                onClick={() => toggle(role.key, page.pageId)}
                                className={`w-6 h-6 rounded flex items-center justify-center mx-auto transition-all ${
                                  granted
                                    ? 'bg-isdb-green text-white hover:bg-isdb-green-dark'
                                    : 'bg-white border-2 border-gray-300 hover:border-isdb-green'
                                }`}
                                title={granted ? `Revoke ${role.label} access to ${page.label}` : `Grant ${role.label} access to ${page.label}`}
                              >
                                {granted && (
                                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                                  </svg>
                                )}
                              </button>
                            </td>
                          )
                        })}
                      </tr>
                    ))}
                  </>
                )
              })}
              {filteredPages.length === 0 && (
                <tr>
                  <td colSpan={ROLES.length + 1} className="text-center py-12 text-gray-400">
                    No pages match your search.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── Legend ── */}
      <div className="flex items-center gap-4 mt-3 text-xs text-gray-500">
        <div className="flex items-center gap-1.5">
          <div className="w-4 h-4 rounded bg-isdb-green" />
          <span>Access granted</span>
        </div>
        <div className="flex items-center gap-1.5">
          <div className="w-4 h-4 rounded border-2 border-gray-300" />
          <span>Access denied</span>
        </div>
      </div>
    </div>
  )
}
