import { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import apiClient from '../../api/client';

interface RegistrationType {
  id: string;
  code: string;
  name: string;
  description?: string;
  isSelectedForSync: boolean;
  isFromEventsAir: boolean;
  sortOrder: number;
  eventsAirId?: string;
}

interface SyncFieldValue {
  id: string;
  value: string;
  isSelectedForSync: boolean;
}

interface SyncFieldMapping {
  id: string;
  displayName: string;
  eventsAirFieldGuid: string;
  description?: string;
  sortOrder: number;
  selectedValues: SyncFieldValue[];
}

export default function RegistrationTypesPage() {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<'registration-types' | string>('registration-types');

  // ── Registration Types state ──────────────────────────────────────────────
  const [selectedTypeIds, setSelectedTypeIds] = useState<Set<string>>(new Set());
  const [typesInitialized, setTypesInitialized] = useState(false);
  const [typesSaveStatus, setTypesSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [typesSaveMsg, setTypesSaveMsg] = useState('');
  const [importStatus, setImportStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle');
  const [importMsg, setImportMsg] = useState('');
  const [showAddForm, setShowAddForm] = useState(false);
  const [newName, setNewName] = useState('');
  const [newCode, setNewCode] = useState('');
  const [newDesc, setNewDesc] = useState('');
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  // ── Field mapping state ───────────────────────────────────────────────────
  const [selectedValueIds, setSelectedValueIds] = useState<Record<string, Set<string>>>({});
  const [fieldSaveStatus, setFieldSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [fieldSaveMsg, setFieldSaveMsg] = useState('');

  // ── Queries ───────────────────────────────────────────────────────────────
  const { data: types = [], isLoading: typesLoading } = useQuery<RegistrationType[]>({
    queryKey: ['registration-types'],
    queryFn: () => apiClient.get('/registration-types').then(r => r.data),
  });

  const { data: fieldMappings = [], isLoading: mappingsLoading } = useQuery<SyncFieldMapping[]>({
    queryKey: ['sync-field-mappings'],
    queryFn: () => apiClient.get('/sync-field-mappings').then(r => r.data),
  });

  // Initialise type selections from server
  useEffect(() => {
    if (types.length > 0 && !typesInitialized) {
      setSelectedTypeIds(new Set(types.filter(t => t.isSelectedForSync).map(t => t.id)));
      setTypesInitialized(true);
    }
  }, [types, typesInitialized]);

  // Initialise field value selections from server
  useEffect(() => {
    if (fieldMappings.length > 0) {
      const init: Record<string, Set<string>> = {};
      fieldMappings.forEach(m => {
        init[m.id] = new Set(m.selectedValues.filter(v => v.isSelectedForSync).map(v => v.id));
      });
      setSelectedValueIds(init);
    }
  }, [fieldMappings]);

  // ── Mutations ─────────────────────────────────────────────────────────────
  const saveTypesMutation = useMutation({
    mutationFn: (ids: string[]) =>
      apiClient.post('/registration-types/sync-selection', { selectedIds: ids }).then(r => r.data),
    onSuccess: (data) => {
      setTypesSaveStatus('saved');
      let msg = data.message || 'Selection saved.';
      if (data.deactivated > 0) msg += ` ${data.deactivated} participant(s) deactivated.`;
      if (data.reactivated > 0) msg += ` ${data.reactivated} participant(s) reactivated.`;
      setTypesSaveMsg(msg);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
      setTimeout(() => setTypesSaveStatus('idle'), 4000);
    },
    onError: () => {
      setTypesSaveStatus('error');
      setTypesSaveMsg('Failed to save selection. Please try again.');
    },
  });

  const addTypeMutation = useMutation({
    mutationFn: () =>
      apiClient.post('/registration-types', { name: newName, code: newCode || undefined, description: newDesc || undefined, isSelectedForSync: false, sortOrder: types.length }),
    onSuccess: () => {
      setNewName(''); setNewCode(''); setNewDesc('');
      setShowAddForm(false);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
    },
  });

  const deleteTypeMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/registration-types/${id}`),
    onSuccess: (_, id) => {
      setSelectedTypeIds(prev => { const n = new Set(prev); n.delete(id); return n; });
      setDeleteConfirm(null);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
    },
  });

  const saveFieldSelectionMutation = useMutation({
    mutationFn: ({ mappingId, ids }: { mappingId: string; ids: string[] }) =>
      apiClient.post(`/sync-field-mappings/${mappingId}/value-selection`, { selectedValueIds: ids }).then(r => r.data),
    onSuccess: (data) => {
      setFieldSaveStatus('saved');
      setFieldSaveMsg(data.message || 'Selection saved.');
      queryClient.invalidateQueries({ queryKey: ['sync-field-mappings'] });
      setTimeout(() => setFieldSaveStatus('idle'), 4000);
    },
    onError: () => {
      setFieldSaveStatus('error');
      setFieldSaveMsg('Failed to save selection.');
    },
  });

  // ── Handlers ──────────────────────────────────────────────────────────────
  const toggleType = (id: string) => {
    setSelectedTypeIds(prev => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n; });
    setTypesSaveStatus('idle');
  };

  const toggleFieldValue = (mappingId: string, valueId: string) => {
    setSelectedValueIds(prev => {
      const cur = new Set(prev[mappingId] || []);
      cur.has(valueId) ? cur.delete(valueId) : cur.add(valueId);
      return { ...prev, [mappingId]: cur };
    });
    setFieldSaveStatus('idle');
  };

  const handleImport = async () => {
    setImportStatus('loading');
    setImportMsg('');
    try {
      const res = await apiClient.post('/registration-types/import-from-eventsair');
      setImportMsg(res.data.message);
      setImportStatus('success');
      setTypesInitialized(false);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
    } catch (err: any) {
      setImportMsg(err?.response?.data?.message || 'Failed to import from EventsAir.');
      setImportStatus('error');
    }
  };

  // ── Tabs ──────────────────────────────────────────────────────────────────
  const tabs = [
    { id: 'registration-types', label: 'Registration Types' },
    ...fieldMappings.map(m => ({ id: m.id, label: m.displayName })),
  ];

  const isLoading = typesLoading || mappingsLoading;
  const hasTypeChanges = typesInitialized && types.some(t => t.isSelectedForSync !== selectedTypeIds.has(t.id));

  // ── Render ────────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600" />
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6 p-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Sync Filters</h1>
          <p className="text-gray-500 mt-1 text-sm">
            Select which registration types and custom field values to include in the EventsAir participant sync.
            Participants matching <strong>any</strong> selected filter will be synced (OR logic).
          </p>
        </div>
        <Link
          to="/integrations/field-mappings"
          className="flex items-center gap-1.5 rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50 whitespace-nowrap"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          Field Mappings
        </Link>
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex gap-4 overflow-x-auto">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`whitespace-nowrap border-b-2 px-1 py-3 text-sm font-medium transition-colors ${
                activeTab === tab.id
                  ? 'border-green-600 text-green-700'
                  : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700'
              }`}
            >
              {tab.label}
              {tab.id === 'registration-types' && (
                <span className="ml-1.5 rounded-full bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">
                  {selectedTypeIds.size}/{types.length}
                </span>
              )}
              {tab.id !== 'registration-types' && (() => {
                const m = fieldMappings.find(fm => fm.id === tab.id);
                if (!m) return null;
                const sel = (selectedValueIds[m.id] || new Set()).size;
                return (
                  <span className="ml-1.5 rounded-full bg-gray-100 px-1.5 py-0.5 text-xs text-gray-500">
                    {sel}/{m.selectedValues.length}
                  </span>
                );
              })()}
            </button>
          ))}
        </nav>
      </div>

      {/* ── Registration Types Tab ── */}
      {activeTab === 'registration-types' && (
        <div className="space-y-4">
          {/* Actions row */}
          <div className="flex flex-wrap items-center gap-2">
            <button
              onClick={handleImport}
              disabled={importStatus === 'loading'}
              className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
            >
              {importStatus === 'loading' ? (
                <><svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"/></svg> Loading…</>
              ) : (
                <><svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M9 19l3 3m0 0l3-3m-3 3V10"/></svg> Load from EventsAir</>
              )}
            </button>
            <button onClick={() => setSelectedTypeIds(new Set(types.map(t => t.id)))} className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50">Select All</button>
            <button onClick={() => setSelectedTypeIds(new Set())} className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50">Clear All</button>
            <button onClick={() => setShowAddForm(v => !v)} className="flex items-center gap-1 rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50">
              <span>+</span> Add Manually
            </button>
            <div className="ml-auto">
              <button
                onClick={() => { setTypesSaveStatus('saving'); saveTypesMutation.mutate(Array.from(selectedTypeIds)); }}
                disabled={!hasTypeChanges || typesSaveStatus === 'saving'}
                className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${hasTypeChanges ? 'bg-green-700 text-white hover:bg-green-800' : 'bg-gray-100 text-gray-400 cursor-not-allowed'}`}
              >
                {typesSaveStatus === 'saving' ? 'Saving…' : 'Save Selection'}
              </button>
            </div>
          </div>

          {/* Import feedback */}
          {importStatus !== 'idle' && importMsg && (
            <div className={`rounded-lg p-3 text-sm flex items-center gap-2 ${importStatus === 'success' ? 'bg-blue-50 border border-blue-200 text-blue-800' : 'bg-red-50 border border-red-200 text-red-700'}`}>
              <span>{importStatus === 'success' ? '✓' : '✗'}</span>
              {importMsg}
              <button onClick={() => { setImportStatus('idle'); setImportMsg(''); }} className="ml-auto opacity-50 hover:opacity-100">✕</button>
            </div>
          )}

          {/* Save feedback */}
          {typesSaveStatus === 'saved' && (
            <div className="rounded-lg bg-green-50 border border-green-200 p-3 text-sm text-green-800">✓ {typesSaveMsg}</div>
          )}
          {typesSaveStatus === 'error' && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">✗ {typesSaveMsg}</div>
          )}

          {/* Add form */}
          {showAddForm && (
            <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
              <h3 className="font-semibold text-gray-800 mb-4">Add New Registration Type</h3>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Name *</label>
                  <input type="text" value={newName} onChange={e => setNewName(e.target.value)} placeholder="e.g. VIP Delegate" className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Code</label>
                  <input type="text" value={newCode} onChange={e => setNewCode(e.target.value.toUpperCase())} placeholder="e.g. VIP" className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-green-500" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                  <input type="text" value={newDesc} onChange={e => setNewDesc(e.target.value)} placeholder="Optional" className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500" />
                </div>
              </div>
              <div className="flex gap-3 mt-4">
                <button onClick={() => addTypeMutation.mutate()} disabled={!newName.trim() || addTypeMutation.isPending} className="bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-green-800 disabled:opacity-50">
                  {addTypeMutation.isPending ? 'Adding…' : 'Add'}
                </button>
                <button onClick={() => setShowAddForm(false)} className="bg-gray-100 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-200">Cancel</button>
              </div>
            </div>
          )}

          {/* Types list */}
          {types.length === 0 ? (
            <div className="rounded-xl border-2 border-dashed border-gray-200 p-16 text-center">
              <div className="text-4xl mb-3">📋</div>
              <p className="font-medium text-gray-500">No registration types yet</p>
              <p className="text-sm mt-1 text-gray-400">Click <strong>Load from EventsAir</strong> to import them automatically.</p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
              <table className="w-full text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="w-10 px-4 py-3"></th>
                    <th className="px-4 py-3 text-left font-medium text-gray-600">Name</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-600">Code</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-600">Source</th>
                    <th className="w-24 px-4 py-3"></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {types.map(type => {
                    const isSelected = selectedTypeIds.has(type.id);
                    return (
                      <tr key={type.id} className={`cursor-pointer hover:bg-gray-50 ${isSelected ? 'bg-green-50' : ''}`} onClick={() => toggleType(type.id)}>
                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                          <div className={`w-5 h-5 rounded border-2 flex items-center justify-center ${isSelected ? 'bg-green-600 border-green-600' : 'border-gray-300 bg-white'}`} onClick={() => toggleType(type.id)}>
                            {isSelected && <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7"/></svg>}
                          </div>
                        </td>
                        <td className="px-4 py-3 font-medium text-gray-900">{type.name}</td>
                        <td className="px-4 py-3 font-mono text-xs text-gray-500">{type.code}</td>
                        <td className="px-4 py-3">
                          {type.isFromEventsAir
                            ? <span className="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">EventsAir</span>
                            : <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">Manual</span>}
                        </td>
                        <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                          {deleteConfirm === type.id ? (
                            <div className="flex items-center gap-1">
                              <button onClick={() => deleteTypeMutation.mutate(type.id)} className="rounded bg-red-600 px-2 py-0.5 text-xs text-white hover:bg-red-700">Confirm</button>
                              <button onClick={() => setDeleteConfirm(null)} className="rounded border border-gray-300 px-2 py-0.5 text-xs text-gray-600">Cancel</button>
                            </div>
                          ) : (
                            <button onClick={() => setDeleteConfirm(type.id)} className="rounded p-1 text-gray-300 hover:bg-red-50 hover:text-red-500">
                              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* ── Custom Field Tabs (e.g. Rank) ── */}
      {activeTab !== 'registration-types' && (() => {
        const mapping = fieldMappings.find(m => m.id === activeTab);
        if (!mapping) return null;
        const curSelected = selectedValueIds[mapping.id] || new Set<string>();
        const hasFieldChanges = mapping.selectedValues.some(v => v.isSelectedForSync !== curSelected.has(v.id));

        return (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm text-gray-500 flex-1">
                {mapping.description || `Select which "${mapping.displayName}" values to include in the participant sync.`}
              </p>
              <button onClick={() => setSelectedValueIds(prev => ({ ...prev, [mapping.id]: new Set(mapping.selectedValues.map(v => v.id)) }))} className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50">Select All</button>
              <button onClick={() => setSelectedValueIds(prev => ({ ...prev, [mapping.id]: new Set() }))} className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50">Clear All</button>
              <button
                onClick={() => { setFieldSaveStatus('saving'); saveFieldSelectionMutation.mutate({ mappingId: mapping.id, ids: Array.from(curSelected) }); }}
                disabled={!hasFieldChanges || fieldSaveStatus === 'saving'}
                className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${hasFieldChanges ? 'bg-green-700 text-white hover:bg-green-800' : 'bg-gray-100 text-gray-400 cursor-not-allowed'}`}
              >
                {fieldSaveStatus === 'saving' ? 'Saving…' : 'Save Selection'}
              </button>
            </div>

            {fieldSaveStatus === 'saved' && <div className="rounded-lg bg-green-50 border border-green-200 p-3 text-sm text-green-800">✓ {fieldSaveMsg}</div>}
            {fieldSaveStatus === 'error' && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">✗ {fieldSaveMsg}</div>}

            {mapping.selectedValues.length === 0 ? (
              <div className="rounded-xl border-2 border-dashed border-gray-200 p-16 text-center">
                <p className="text-gray-500">No values loaded yet for "{mapping.displayName}".</p>
                <p className="mt-1 text-sm text-gray-400">
                  Go to{' '}
                  <Link to="/integrations/field-mappings" className="text-blue-600 hover:underline">Field Mappings</Link>
                  {' '}and click "Load Values" to discover values from EventsAir.
                </p>
              </div>
            ) : (
              <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="w-10 px-4 py-3"></th>
                      <th className="px-4 py-3 text-left font-medium text-gray-600">{mapping.displayName}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {mapping.selectedValues
                      .sort((a, b) => a.value.localeCompare(b.value))
                      .map(val => {
                        const isSelected = curSelected.has(val.id);
                        return (
                          <tr key={val.id} className={`cursor-pointer hover:bg-gray-50 ${isSelected ? 'bg-green-50' : ''}`} onClick={() => toggleFieldValue(mapping.id, val.id)}>
                            <td className="px-4 py-3">
                              <div className={`w-5 h-5 rounded border-2 flex items-center justify-center ${isSelected ? 'bg-green-600 border-green-600' : 'border-gray-300 bg-white'}`}>
                                {isSelected && <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7"/></svg>}
                              </div>
                            </td>
                            <td className="px-4 py-3 font-medium text-gray-900">{val.value}</td>
                          </tr>
                        );
                      })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        );
      })()}
    </div>
  );
}
