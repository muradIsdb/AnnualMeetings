import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2, RefreshCw, Edit2, Check, X, Info } from 'lucide-react'
import apiClient from '../../api/client'

interface SyncFieldValue {
  id: string
  value: string
  isSelectedForSync: boolean
}

interface SyncFieldMapping {
  id: string
  displayName: string
  eventsAirFieldGuid: string
  description?: string
  sortOrder: number
  createdAt: string
  updatedAt: string
  selectedValues: SyncFieldValue[]
}

export default function FieldMappingsPage() {
  const qc = useQueryClient()
  const [showAddForm, setShowAddForm] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [newMapping, setNewMapping] = useState({ displayName: '', eventsAirFieldGuid: '', description: '' })
  const [editForm, setEditForm] = useState({ displayName: '', eventsAirFieldGuid: '', description: '' })
  const [loadingValues, setLoadingValues] = useState<Record<string, boolean>>({})
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null)

  const { data: mappings = [], isLoading } = useQuery<SyncFieldMapping[]>({
    queryKey: ['sync-field-mappings'],
    queryFn: () => apiClient.get('/sync-field-mappings').then(r => r.data),
  })

  const showFeedback = (type: 'success' | 'error', message: string) => {
    setFeedback({ type, message })
    setTimeout(() => setFeedback(null), 4000)
  }

  const createMutation = useMutation({
    mutationFn: (data: typeof newMapping) => apiClient.post('/sync-field-mappings', data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sync-field-mappings'] })
      setShowAddForm(false)
      setNewMapping({ displayName: '', eventsAirFieldGuid: '', description: '' })
      showFeedback('success', 'Field mapping created successfully.')
    },
    onError: (err: any) => showFeedback('error', err.response?.data?.message || 'Failed to create field mapping.'),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: typeof editForm }) => apiClient.put(`/sync-field-mappings/${id}`, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sync-field-mappings'] })
      setEditingId(null)
      showFeedback('success', 'Field mapping updated.')
    },
    onError: (err: any) => showFeedback('error', err.response?.data?.message || 'Failed to update.'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/sync-field-mappings/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['sync-field-mappings'] })
      showFeedback('success', 'Field mapping deleted.')
    },
    onError: () => showFeedback('error', 'Failed to delete field mapping.'),
  })

  const loadValues = async (mapping: SyncFieldMapping) => {
    setLoadingValues(prev => ({ ...prev, [mapping.id]: true }))
    try {
      const res = await apiClient.post(`/sync-field-mappings/${mapping.id}/load-values-from-eventsair`)
      qc.invalidateQueries({ queryKey: ['sync-field-mappings'] })
      showFeedback('success', res.data.message)
    } catch (err: any) {
      showFeedback('error', err.response?.data?.message || 'Failed to load values from EventsAir.')
    } finally {
      setLoadingValues(prev => ({ ...prev, [mapping.id]: false }))
    }
  }

  const startEdit = (m: SyncFieldMapping) => {
    setEditingId(m.id)
    setEditForm({ displayName: m.displayName, eventsAirFieldGuid: m.eventsAirFieldGuid, description: m.description || '' })
  }

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Field Mapping Configuration</h1>
        <p className="mt-1 text-sm text-gray-500">
          Configure which EventsAir custom contact fields are used as sync filters.
          Each field appears as a tab on the Registration Types page, allowing you to select specific values to include in the participant sync.
        </p>
      </div>

      {/* Info banner */}
      <div className="mb-6 flex items-start gap-3 rounded-lg border border-blue-200 bg-blue-50 p-4">
        <Info className="mt-0.5 h-5 w-5 shrink-0 text-blue-500" />
        <div className="text-sm text-blue-800">
          <p className="font-medium">How it works</p>
          <p className="mt-1">
            Add a field by entering its <strong>Display Name</strong> (shown as a tab) and its <strong>EventsAir Field GUID</strong>.
            Then click <strong>Load Values from EventsAir</strong> to discover all unique values for that field across all registrations.
            Finally, go to the <strong>Registration Types</strong> page to select which values to include in the sync.
          </p>
          <p className="mt-1">
            The sync uses <strong>OR logic</strong>: a participant is included if their Registration Type is selected <em>or</em> if any of their custom field values match.
          </p>
        </div>
      </div>

      {/* Feedback */}
      {feedback && (
        <div className={`mb-4 rounded-lg p-3 text-sm font-medium ${
          feedback.type === 'success' ? 'bg-green-50 text-green-800 border border-green-200' : 'bg-red-50 text-red-800 border border-red-200'
        }`}>
          {feedback.message}
        </div>
      )}

      {/* Add new mapping */}
      <div className="mb-6">
        {!showAddForm ? (
          <button
            onClick={() => setShowAddForm(true)}
            className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            <Plus className="h-4 w-4" />
            Add Field Mapping
          </button>
        ) : (
          <div className="rounded-lg border border-blue-200 bg-blue-50 p-4">
            <h3 className="mb-3 text-sm font-semibold text-gray-800">New Field Mapping</h3>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-700">Display Name *</label>
                <input
                  type="text"
                  placeholder="e.g. Rank"
                  value={newMapping.displayName}
                  onChange={e => setNewMapping(p => ({ ...p, displayName: e.target.value }))}
                  className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-700">EventsAir Field GUID *</label>
                <input
                  type="text"
                  placeholder="e.g. 3d96b87e-87b0-145e-5f45-3a17bafe26d4"
                  value={newMapping.eventsAirFieldGuid}
                  onChange={e => setNewMapping(p => ({ ...p, eventsAirFieldGuid: e.target.value }))}
                  className="w-full rounded border border-gray-300 px-3 py-2 text-sm font-mono focus:border-blue-500 focus:outline-none"
                />
              </div>
              <div className="sm:col-span-2">
                <label className="mb-1 block text-xs font-medium text-gray-700">Description (optional)</label>
                <input
                  type="text"
                  placeholder="Brief description of what this field represents"
                  value={newMapping.description}
                  onChange={e => setNewMapping(p => ({ ...p, description: e.target.value }))}
                  className="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
                />
              </div>
            </div>
            <div className="mt-3 flex gap-2">
              <button
                onClick={() => createMutation.mutate(newMapping)}
                disabled={!newMapping.displayName || !newMapping.eventsAirFieldGuid || createMutation.isPending}
                className="flex items-center gap-1 rounded bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                <Check className="h-4 w-4" />
                {createMutation.isPending ? 'Saving...' : 'Save'}
              </button>
              <button
                onClick={() => { setShowAddForm(false); setNewMapping({ displayName: '', eventsAirFieldGuid: '', description: '' }) }}
                className="flex items-center gap-1 rounded border border-gray-300 px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-50"
              >
                <X className="h-4 w-4" />
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Mappings list */}
      {isLoading ? (
        <div className="text-center py-12 text-gray-400">Loading field mappings...</div>
      ) : mappings.length === 0 ? (
        <div className="rounded-lg border-2 border-dashed border-gray-200 p-12 text-center">
          <p className="text-gray-500">No field mappings configured yet.</p>
          <p className="mt-1 text-sm text-gray-400">
            Add a field mapping to enable filtering by custom EventsAir contact fields.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {mappings.map(mapping => (
            <div key={mapping.id} className="rounded-lg border border-gray-200 bg-white shadow-sm">
              <div className="flex items-start justify-between p-4">
                <div className="flex-1">
                  {editingId === mapping.id ? (
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                      <div>
                        <label className="mb-1 block text-xs font-medium text-gray-600">Display Name</label>
                        <input
                          type="text"
                          value={editForm.displayName}
                          onChange={e => setEditForm(p => ({ ...p, displayName: e.target.value }))}
                          className="w-full rounded border border-gray-300 px-2 py-1 text-sm focus:border-blue-500 focus:outline-none"
                        />
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-medium text-gray-600">EventsAir Field GUID</label>
                        <input
                          type="text"
                          value={editForm.eventsAirFieldGuid}
                          onChange={e => setEditForm(p => ({ ...p, eventsAirFieldGuid: e.target.value }))}
                          className="w-full rounded border border-gray-300 px-2 py-1 text-sm font-mono focus:border-blue-500 focus:outline-none"
                        />
                      </div>
                      <div className="sm:col-span-2">
                        <label className="mb-1 block text-xs font-medium text-gray-600">Description</label>
                        <input
                          type="text"
                          value={editForm.description}
                          onChange={e => setEditForm(p => ({ ...p, description: e.target.value }))}
                          className="w-full rounded border border-gray-300 px-2 py-1 text-sm focus:border-blue-500 focus:outline-none"
                        />
                      </div>
                      <div className="sm:col-span-2 flex gap-2 mt-1">
                        <button
                          onClick={() => updateMutation.mutate({ id: mapping.id, data: editForm })}
                          disabled={updateMutation.isPending}
                          className="flex items-center gap-1 rounded bg-blue-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50"
                        >
                          <Check className="h-3 w-3" /> Save
                        </button>
                        <button
                          onClick={() => setEditingId(null)}
                          className="flex items-center gap-1 rounded border border-gray-300 px-2.5 py-1 text-xs text-gray-600 hover:bg-gray-50"
                        >
                          <X className="h-3 w-3" /> Cancel
                        </button>
                      </div>
                    </div>
                  ) : (
                    <>
                      <div className="flex items-center gap-2">
                        <h3 className="text-base font-semibold text-gray-900">{mapping.displayName}</h3>
                        <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">
                          {mapping.selectedValues.filter(v => v.isSelectedForSync).length} / {mapping.selectedValues.length} selected
                        </span>
                      </div>
                      <p className="mt-0.5 font-mono text-xs text-gray-400">{mapping.eventsAirFieldGuid}</p>
                      {mapping.description && (
                        <p className="mt-1 text-sm text-gray-500">{mapping.description}</p>
                      )}
                      {mapping.selectedValues.length > 0 && (
                        <div className="mt-2 flex flex-wrap gap-1">
                          {mapping.selectedValues
                            .filter(v => v.isSelectedForSync)
                            .slice(0, 8)
                            .map(v => (
                              <span key={v.id} className="rounded-full bg-green-100 px-2 py-0.5 text-xs text-green-700">
                                {v.value}
                              </span>
                            ))}
                          {mapping.selectedValues.filter(v => v.isSelectedForSync).length > 8 && (
                            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">
                              +{mapping.selectedValues.filter(v => v.isSelectedForSync).length - 8} more
                            </span>
                          )}
                        </div>
                      )}
                    </>
                  )}
                </div>

                {editingId !== mapping.id && (
                  <div className="ml-4 flex items-center gap-2">
                    <button
                      onClick={() => loadValues(mapping)}
                      disabled={loadingValues[mapping.id]}
                      title="Load values from EventsAir"
                      className="flex items-center gap-1 rounded border border-gray-300 px-2.5 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
                    >
                      <RefreshCw className={`h-3.5 w-3.5 ${loadingValues[mapping.id] ? 'animate-spin' : ''}`} />
                      {loadingValues[mapping.id] ? 'Loading...' : 'Load Values'}
                    </button>
                    <button
                      onClick={() => startEdit(mapping)}
                      title="Edit"
                      className="rounded p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
                    >
                      <Edit2 className="h-4 w-4" />
                    </button>
                    <button
                      onClick={() => {
                        if (confirm(`Delete field mapping "${mapping.displayName}"? This will also remove all its loaded values.`))
                          deleteMutation.mutate(mapping.id)
                      }}
                      title="Delete"
                      className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-500"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                )}
              </div>

              {/* Values list */}
              {mapping.selectedValues.length > 0 && editingId !== mapping.id && (
                <div className="border-t border-gray-100 px-4 py-3">
                  <p className="mb-2 text-xs font-medium text-gray-500 uppercase tracking-wide">
                    Available Values — select on the Registration Types page
                  </p>
                  <div className="flex flex-wrap gap-1.5">
                    {mapping.selectedValues
                      .sort((a, b) => a.value.localeCompare(b.value))
                      .map(v => (
                        <span
                          key={v.id}
                          className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                            v.isSelectedForSync
                              ? 'bg-green-100 text-green-700 ring-1 ring-green-300'
                              : 'bg-gray-100 text-gray-500'
                          }`}
                        >
                          {v.value}
                        </span>
                      ))}
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
