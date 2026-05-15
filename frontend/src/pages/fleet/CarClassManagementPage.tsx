import { useState, useEffect, useCallback } from 'react'
import {
  Plus,
  Pencil,
  Trash2,
  AlertTriangle,
  Car,
  Users,
  History,
  X,
  ChevronLeft,
  ChevronRight,
  ArrowRight,
  Clock,
  Filter,
} from 'lucide-react'
import apiClient from '../../api/client'

// ─── Types ─────────────────────────────────────────────────────────────────────

interface CarClassSummary {
  id: string
  name: string
  description?: string
  color?: string
  sortOrder: number
  vehicleCount: number
  guestCount: number
}

interface CarClassFormState {
  name: string
  description: string
  color: string
  sortOrder: number
}

interface HistoryItem {
  id: string
  vehicleId: string
  vehiclePlate: string
  vehicleMake: string
  vehicleModel: string
  oldStatus: string
  newStatus: string
  changedByName?: string
  changedByRole?: string
  notes?: string
  changedAt: string
}

interface HistoryPage {
  carClassId: string
  carClassName: string
  total: number
  page: number
  pageSize: number
  items: HistoryItem[]
}

// ─── Constants ─────────────────────────────────────────────────────────────────

const PRESET_COLORS = [
  '#7C3AED', '#4F46E5', '#0EA5E9', '#10B981',
  '#F59E0B', '#EF4444', '#EC4899', '#6B7280',
  '#1D4ED8', '#065F46', '#92400E', '#1E3A5F',
]

const EMPTY_FORM: CarClassFormState = {
  name: '',
  description: '',
  color: '#7C3AED',
  sortOrder: 1,
}

// ─── Status badge helper ────────────────────────────────────────────────────────

const STATUS_COLORS: Record<string, string> = {
  Available:   'bg-green-100 text-green-800',
  Assigned:    'bg-blue-100 text-blue-800',
  OutOfService:'bg-red-100 text-red-800',
  NotProvided: 'bg-gray-100 text-gray-600',
}

function StatusBadge({ status }: { status: string }) {
  const cls = STATUS_COLORS[status] ?? 'bg-gray-100 text-gray-600'
  const label = status === 'OutOfService' ? 'Out of Service'
              : status === 'NotProvided'  ? 'Not Provided'
              : status
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${cls}`}>
      {label}
    </span>
  )
}

// ─── History Modal ──────────────────────────────────────────────────────────────

function CarClassHistoryModal({
  carClass,
  onClose,
}: {
  carClass: CarClassSummary
  onClose: () => void
}) {
  const [data, setData]       = useState<HistoryPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState<string | null>(null)
  const [page, setPage]       = useState(1)
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate]     = useState('')
  const [filterApplied, setFilterApplied] = useState(false)

  const load = useCallback(
    async (p: number, from: string, to: string) => {
      setLoading(true)
      setError(null)
      try {
        const params: Record<string, string | number> = { page: p, pageSize: 20 }
        if (from) params.fromDate = from
        if (to)   params.toDate   = to
        const { data: res } = await apiClient.get<HistoryPage>(
          `/car-classes/${carClass.id}/history`,
          { params }
        )
        setData(res)
      } catch {
        setError('Failed to load history.')
      } finally {
        setLoading(false)
      }
    },
    [carClass.id]
  )

  useEffect(() => { load(1, '', '') }, [load])

  const applyFilter = () => {
    setPage(1)
    setFilterApplied(true)
    load(1, fromDate, toDate)
  }

  const clearFilter = () => {
    setFromDate('')
    setToDate('')
    setFilterApplied(false)
    setPage(1)
    load(1, '', '')
  }

  const goToPage = (p: number) => {
    setPage(p)
    load(p, fromDate, toDate)
  }

  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 0

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-3 sm:p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="p-4 sm:p-6 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className="w-3 h-8 rounded-full flex-shrink-0"
              style={{ backgroundColor: carClass.color ?? '#9CA3AF' }}
            />
            <div className="min-w-0">
              <h2 className="text-base sm:text-lg font-semibold text-gray-900 truncate">
                {carClass.name} — Provision History
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                Vehicle status changes over time
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors flex-shrink-0 ml-2"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Date filter */}
        <div className="px-4 sm:px-6 py-3 border-b border-gray-100 bg-gray-50 flex-shrink-0">
          <div className="flex flex-col sm:flex-row gap-2 sm:items-end">
            <div className="flex gap-2 flex-1">
              <div className="flex-1">
                <label className="block text-xs font-medium text-gray-600 mb-1">From</label>
                <input
                  type="date"
                  value={fromDate}
                  onChange={e => setFromDate(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
              <div className="flex-1">
                <label className="block text-xs font-medium text-gray-600 mb-1">To</label>
                <input
                  type="date"
                  value={toDate}
                  onChange={e => setToDate(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
            </div>
            <div className="flex gap-2">
              <button
                onClick={applyFilter}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-isdb-green text-white rounded-lg text-sm font-medium hover:bg-isdb-green/90 transition-colors"
              >
                <Filter className="w-3.5 h-3.5" />
                Filter
              </button>
              {filterApplied && (
                <button
                  onClick={clearFilter}
                  className="px-3 py-1.5 border border-gray-300 text-gray-600 rounded-lg text-sm hover:bg-gray-50 transition-colors"
                >
                  Clear
                </button>
              )}
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-4 sm:p-6">
          {loading ? (
            <div className="text-center py-12 text-gray-400">
              <Clock className="w-8 h-8 mx-auto mb-2 animate-pulse" />
              <p className="text-sm">Loading history…</p>
            </div>
          ) : error ? (
            <div className="text-center py-12 text-red-500 text-sm">{error}</div>
          ) : !data || data.items.length === 0 ? (
            <div className="text-center py-12 text-gray-400">
              <History className="w-10 h-10 mx-auto mb-3 opacity-30" />
              <p className="text-sm">No history records found.</p>
              {filterApplied && (
                <button onClick={clearFilter} className="mt-2 text-isdb-green hover:underline text-xs">
                  Clear filter
                </button>
              )}
            </div>
          ) : (
            <div className="space-y-2">
              {data.items.map(item => (
                <div
                  key={item.id}
                  className="bg-gray-50 border border-gray-200 rounded-xl p-3 sm:p-4"
                >
                  {/* Vehicle + status change row */}
                  <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3">
                    <div className="flex items-center gap-2 flex-1 min-w-0">
                      <Car className="w-4 h-4 text-gray-400 flex-shrink-0" />
                      <span className="font-medium text-gray-900 text-sm truncate">
                        {item.vehicleMake} {item.vehicleModel}
                      </span>
                      <span className="text-xs text-gray-500 font-mono flex-shrink-0">
                        {item.vehiclePlate}
                      </span>
                    </div>
                    {/* Status transition */}
                    <div className="flex items-center gap-1.5 flex-shrink-0">
                      <StatusBadge status={item.oldStatus} />
                      <ArrowRight className="w-3.5 h-3.5 text-gray-400 flex-shrink-0" />
                      <StatusBadge status={item.newStatus} />
                    </div>
                  </div>

                  {/* Meta row */}
                  <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-500">
                    <span className="flex items-center gap-1">
                      <Clock className="w-3 h-3" />
                      {new Date(item.changedAt).toLocaleString('en-GB', {
                        day: '2-digit', month: 'short', year: 'numeric',
                        hour: '2-digit', minute: '2-digit',
                      })}
                    </span>
                    {item.changedByName && (
                      <span>By: <span className="text-gray-700">{item.changedByName}</span>
                        {item.changedByRole && ` (${item.changedByRole})`}
                      </span>
                    )}
                    {item.notes && (
                      <span className="italic text-gray-400">"{item.notes}"</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Pagination */}
        {data && totalPages > 1 && (
          <div className="px-4 sm:px-6 py-3 border-t border-gray-100 flex items-center justify-between flex-shrink-0">
            <span className="text-xs text-gray-500">
              {data.total} record{data.total !== 1 ? 's' : ''} · Page {page} of {totalPages}
            </span>
            <div className="flex gap-2">
              <button
                onClick={() => goToPage(page - 1)}
                disabled={page <= 1}
                className="p-1.5 rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button
                onClick={() => goToPage(page + 1)}
                disabled={page >= totalPages}
                className="p-1.5 rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

// ─── Main Page ──────────────────────────────────────────────────────────────────

export default function CarClassManagementPage() {
  const [classes, setClasses]           = useState<CarClassSummary[]>([])
  const [loading, setLoading]           = useState(true)
  const [showForm, setShowForm]         = useState(false)
  const [editId, setEditId]             = useState<string | null>(null)
  const [form, setForm]                 = useState<CarClassFormState>(EMPTY_FORM)
  const [saving, setSaving]             = useState(false)
  const [formError, setFormError]       = useState<string | null>(null)
  const [pageError, setPageError]       = useState<string | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<CarClassSummary | null>(null)
  const [deleting, setDeleting]         = useState(false)
  const [historyTarget, setHistoryTarget] = useState<CarClassSummary | null>(null)

  const load = async () => {
    try {
      setLoading(true)
      const { data } = await apiClient.get<CarClassSummary[]>('/car-classes')
      setClasses(data)
    } catch {
      setPageError('Failed to load car classes.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  const openNew = () => {
    setForm({ ...EMPTY_FORM, sortOrder: classes.length + 1 })
    setEditId(null)
    setFormError(null)
    setShowForm(true)
  }

  const openEdit = (c: CarClassSummary) => {
    setForm({
      name: c.name,
      description: c.description ?? '',
      color: c.color ?? '#7C3AED',
      sortOrder: c.sortOrder,
    })
    setEditId(c.id)
    setFormError(null)
    setShowForm(true)
  }

  const save = async () => {
    if (!form.name.trim()) { setFormError('Name is required.'); return }
    setSaving(true)
    setFormError(null)
    try {
      const body = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        color: form.color || null,
        sortOrder: form.sortOrder,
      }
      if (editId) {
        await apiClient.put(`/car-classes/${editId}`, body)
      } else {
        await apiClient.post('/car-classes', body)
      }
      setShowForm(false)
      load()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setFormError(msg ?? 'Failed to save car class.')
    } finally {
      setSaving(false)
    }
  }

  const confirmDelete = async () => {
    if (!deleteTarget) return
    setDeleting(true)
    try {
      await apiClient.delete(`/car-classes/${deleteTarget.id}`)
      setDeleteTarget(null)
      load()
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
      setPageError(msg ?? 'Failed to delete car class.')
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  return (
    <div className="p-4 sm:p-6 max-w-4xl mx-auto">
      {/* Page header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mb-6">
        <div>
          <h1 className="text-xl sm:text-2xl font-bold text-gray-900">Car Classes</h1>
          <p className="text-sm text-gray-500 mt-1">
            Define vehicle classes and assign them to guests to control which cars appear during vehicle assignment.
          </p>
        </div>
        <button
          onClick={openNew}
          className="flex items-center justify-center gap-2 px-4 py-2.5 bg-isdb-green text-white rounded-lg hover:bg-isdb-green/90 transition-colors text-sm font-medium w-full sm:w-auto"
        >
          <Plus className="w-4 h-4" />
          New Class
        </button>
      </div>

      {/* Page-level error */}
      {pageError && !showForm && (
        <div className="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 flex-shrink-0" />
          {pageError}
        </div>
      )}

      {/* List */}
      {loading ? (
        <div className="text-center py-12 text-gray-400 text-sm">Loading…</div>
      ) : classes.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Car className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">No car classes defined yet.</p>
          <button onClick={openNew} className="mt-3 text-isdb-green hover:underline text-sm">
            Add the first class
          </button>
        </div>
      ) : (
        <div className="space-y-3">
          {classes.map(c => (
            <div
              key={c.id}
              className="bg-white border border-gray-200 rounded-xl p-4 shadow-sm hover:shadow-md transition-shadow"
            >
              {/* Top row: color bar + name + actions */}
              <div className="flex items-start gap-3">
                {/* Color bar */}
                <div
                  className="w-1.5 self-stretch rounded-full flex-shrink-0 min-h-[2.5rem]"
                  style={{ backgroundColor: c.color ?? '#9CA3AF' }}
                />

                {/* Name + description */}
                <div className="flex-1 min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold text-gray-900">{c.name}</span>
                    <span
                      className="px-2 py-0.5 rounded-full text-xs font-medium text-white"
                      style={{ backgroundColor: c.color ?? '#9CA3AF' }}
                    >
                      Class
                    </span>
                  </div>
                  {c.description && (
                    <p className="text-sm text-gray-500 mt-0.5 line-clamp-2">{c.description}</p>
                  )}
                </div>

                {/* Action buttons — always visible, stacked on very small screens */}
                <div className="flex items-center gap-1 flex-shrink-0">
                  <button
                    onClick={() => setHistoryTarget(c)}
                    className="p-2 text-gray-400 hover:text-purple-600 hover:bg-purple-50 rounded-lg transition-colors"
                    title="View History"
                    aria-label="View provision history"
                  >
                    <History className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => openEdit(c)}
                    className="p-2 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                    title="Edit"
                    aria-label="Edit car class"
                  >
                    <Pencil className="w-4 h-4" />
                  </button>
                  <button
                    onClick={() => { setPageError(null); setDeleteTarget(c) }}
                    className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
                    title="Delete"
                    aria-label="Delete car class"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </div>

              {/* Stats row */}
              <div className="mt-3 flex flex-wrap gap-4 text-sm text-gray-500 pl-4">
                <div className="flex items-center gap-1.5">
                  <Car className="w-4 h-4" />
                  <span>{c.vehicleCount} vehicle{c.vehicleCount !== 1 ? 's' : ''}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <Users className="w-4 h-4" />
                  <span>{c.guestCount} guest{c.guestCount !== 1 ? 's' : ''}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Create / Edit Modal ── */}
      {showForm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-3 sm:p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col">
            <div className="p-5 sm:p-6 border-b border-gray-100 flex-shrink-0">
              <h2 className="text-lg font-semibold text-gray-900">
                {editId ? 'Edit Car Class' : 'New Car Class'}
              </h2>
            </div>
            <div className="p-5 sm:p-6 space-y-4 overflow-y-auto flex-1">
              {formError && (
                <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-sm">
                  {formError}
                </div>
              )}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={form.name}
                  onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                  placeholder="e.g. Luxury Car"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea
                  value={form.description}
                  onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                  placeholder="Optional description…"
                  rows={2}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green resize-none"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">Badge Color</label>
                <div className="flex flex-wrap items-center gap-2">
                  {PRESET_COLORS.map(col => (
                    <button
                      key={col}
                      onClick={() => setForm(f => ({ ...f, color: col }))}
                      className={`w-8 h-8 rounded-full transition-transform hover:scale-110 ${
                        form.color === col ? 'ring-2 ring-offset-2 ring-gray-400 scale-110' : ''
                      }`}
                      style={{ backgroundColor: col }}
                      title={col}
                    />
                  ))}
                  <input
                    type="color"
                    value={form.color}
                    onChange={e => setForm(f => ({ ...f, color: e.target.value }))}
                    className="w-8 h-8 rounded-full cursor-pointer border border-gray-300"
                    title="Custom color"
                  />
                </div>
                <div className="mt-2 flex items-center gap-2">
                  <span
                    className="px-3 py-1 rounded-full text-xs font-medium text-white"
                    style={{ backgroundColor: form.color }}
                  >
                    {form.name || 'Preview'}
                  </span>
                  <span className="text-xs text-gray-400">Preview</span>
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Sort Order</label>
                <input
                  type="number"
                  value={form.sortOrder}
                  onChange={e => setForm(f => ({ ...f, sortOrder: parseInt(e.target.value) || 0 }))}
                  min={0}
                  className="w-24 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
                />
                <p className="text-xs text-gray-400 mt-1">Lower numbers appear first.</p>
              </div>
            </div>
            <div className="p-5 sm:p-6 border-t border-gray-100 flex justify-end gap-3 flex-shrink-0">
              <button
                onClick={() => setShowForm(false)}
                className="px-4 py-2 text-sm text-gray-600 hover:text-gray-900 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={save}
                disabled={saving}
                className="px-4 py-2 text-sm font-medium bg-isdb-green text-white rounded-lg hover:bg-isdb-green/90 disabled:opacity-50 transition-colors"
              >
                {saving ? 'Saving…' : editId ? 'Save Changes' : 'Create Class'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Delete Confirm Modal ── */}
      {deleteTarget && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-3 sm:p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm p-5 sm:p-6">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center flex-shrink-0">
                <Trash2 className="w-5 h-5 text-red-600" />
              </div>
              <div>
                <h3 className="font-semibold text-gray-900">Delete Car Class</h3>
                <p className="text-sm text-gray-500">This action cannot be undone.</p>
              </div>
            </div>
            <p className="text-sm text-gray-700 mb-1">
              Are you sure you want to delete <strong>"{deleteTarget.name}"</strong>?
            </p>
            {(deleteTarget.vehicleCount > 0 || deleteTarget.guestCount > 0) && (
              <div className="mt-3 p-3 bg-amber-50 border border-amber-200 rounded-lg text-amber-700 text-sm flex items-start gap-2">
                <AlertTriangle className="w-4 h-4 flex-shrink-0 mt-0.5" />
                <span>
                  This class has {deleteTarget.vehicleCount} vehicle(s) and{' '}
                  {deleteTarget.guestCount} guest(s) assigned. You must reassign them before deleting.
                </span>
              </div>
            )}
            <div className="flex justify-end gap-3 mt-5">
              <button
                onClick={() => setDeleteTarget(null)}
                className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={confirmDelete}
                disabled={deleting || deleteTarget.vehicleCount > 0 || deleteTarget.guestCount > 0}
                className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 transition-colors"
              >
                {deleting ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── History Modal ── */}
      {historyTarget && (
        <CarClassHistoryModal
          carClass={historyTarget}
          onClose={() => setHistoryTarget(null)}
        />
      )}
    </div>
  )
}
