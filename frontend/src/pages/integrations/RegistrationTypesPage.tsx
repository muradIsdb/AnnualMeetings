import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import apiClient from '../../api/client';

interface RegistrationType {
  id: string;
  code: string;
  name: string;
  description?: string;
  isSelectedForSync: boolean;
  isFromEventsAir: boolean;
  sortOrder: number;
}

const fetchRegistrationTypes = async (): Promise<RegistrationType[]> => {
  const res = await apiClient.get('/registration-types');
  return res.data;
};

export default function RegistrationTypesPage() {
  const queryClient = useQueryClient();
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [initialized, setInitialized] = useState(false);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [saveMessage, setSaveMessage] = useState('');

  // Add form state
  const [showAddForm, setShowAddForm] = useState(false);
  const [newName, setNewName] = useState('');
  const [newCode, setNewCode] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [addError, setAddError] = useState('');

  const { data: types = [] as RegistrationType[], isLoading, error } = useQuery<RegistrationType[]>({
    queryKey: ['registration-types'],
    queryFn: fetchRegistrationTypes,
  });

  useEffect(() => {
    if (types.length > 0 && !initialized) {
      const preSelected = new Set(
        types.filter((t: RegistrationType) => t.isSelectedForSync).map((t: RegistrationType) => t.id)
      );
      setSelectedIds(preSelected);
      setInitialized(true);
    }
  }, [types, initialized]);

  const saveMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      const res = await apiClient.post('/registration-types/sync-selection', {
        selectedIds: ids,
      });
      return res.data;
    },
    onSuccess: (data) => {
      setSaveStatus('saved');
      setSaveMessage(data.message);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
      setTimeout(() => setSaveStatus('idle'), 3000);
    },
    onError: () => {
      setSaveStatus('error');
      setSaveMessage('Failed to save selection. Please try again.');
    },
  });

  const addMutation = useMutation({
    mutationFn: async () => {
      const res = await apiClient.post('/registration-types', {
        name: newName,
        code: newCode || undefined,
        description: newDescription || undefined,
        isSelectedForSync: false,
        sortOrder: types.length,
      });
      return res.data;
    },
    onSuccess: () => {
      setNewName('');
      setNewCode('');
      setNewDescription('');
      setAddError('');
      setShowAddForm(false);
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
    },
    onError: (err: any) => {
      setAddError(err?.response?.data?.message || 'Failed to add registration type.');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/registration-types/${id}`);
    },
    onSuccess: (_, id) => {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      queryClient.invalidateQueries({ queryKey: ['registration-types'] });
    },
  });

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
    setSaveStatus('idle');
  };

  const selectAll = () => {
    setSelectedIds(new Set(types.map((t) => t.id)));
    setSaveStatus('idle');
  };

  const clearAll = () => {
    setSelectedIds(new Set());
    setSaveStatus('idle');
  };

  const handleSave = () => {
    setSaveStatus('saving');
    saveMutation.mutate(Array.from(selectedIds));
  };

  const hasChanges = initialized && types.some(
    (t) => t.isSelectedForSync !== selectedIds.has(t.id)
  );

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-green-600" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
        Failed to load registration types. Please refresh the page.
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Registration Types</h1>
          <p className="text-gray-500 mt-1">
            Select which registration types should be synchronised from EventsAir. Only guests with selected types will be imported.
          </p>
        </div>
        <button
          onClick={() => setShowAddForm(!showAddForm)}
          className="flex items-center gap-2 bg-green-700 text-white px-4 py-2 rounded-lg hover:bg-green-800 transition-colors text-sm font-medium"
        >
          <span>+</span> Add Type
        </button>
      </div>

      {/* Add Form */}
      {showAddForm && (
        <div className="bg-white border border-gray-200 rounded-xl p-5 shadow-sm">
          <h3 className="font-semibold text-gray-800 mb-4">Add New Registration Type</h3>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="e.g. VIP Delegate"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Code <span className="text-gray-400 text-xs">(auto-generated if blank)</span>
              </label>
              <input
                type="text"
                value={newCode}
                onChange={(e) => setNewCode(e.target.value.toUpperCase())}
                placeholder="e.g. VIP_DELEGATE"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 font-mono"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
              <input
                type="text"
                value={newDescription}
                onChange={(e) => setNewDescription(e.target.value)}
                placeholder="Optional description"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              />
            </div>
          </div>
          {addError && (
            <p className="text-red-600 text-sm mt-2">{addError}</p>
          )}
          <div className="flex gap-3 mt-4">
            <button
              onClick={() => addMutation.mutate()}
              disabled={!newName.trim() || addMutation.isPending}
              className="bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-green-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {addMutation.isPending ? 'Adding...' : 'Add Registration Type'}
            </button>
            <button
              onClick={() => { setShowAddForm(false); setAddError(''); }}
              className="bg-gray-100 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-200 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {/* Selection Controls */}
      <div className="bg-white border border-gray-200 rounded-xl shadow-sm">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div className="flex items-center gap-4">
            <span className="text-sm text-gray-600">
              <span className="font-semibold text-green-700">{selectedIds.size}</span> of{' '}
              <span className="font-semibold">{types.length}</span> types selected for sync
            </span>
            <div className="flex gap-2">
              <button
                onClick={selectAll}
                className="text-xs text-green-700 hover:text-green-900 font-medium underline"
              >
                Select All
              </button>
              <span className="text-gray-300">|</span>
              <button
                onClick={clearAll}
                className="text-xs text-gray-500 hover:text-gray-700 font-medium underline"
              >
                Clear All
              </button>
            </div>
          </div>
          <button
            onClick={handleSave}
            disabled={!hasChanges || saveStatus === 'saving'}
            className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
              hasChanges
                ? 'bg-green-700 text-white hover:bg-green-800'
                : 'bg-gray-100 text-gray-400 cursor-not-allowed'
            }`}
          >
            {saveStatus === 'saving' ? 'Saving...' : 'Save Selection'}
          </button>
        </div>

        {/* Save status banner */}
        {saveStatus === 'saved' && (
          <div className="bg-green-50 border-b border-green-100 px-5 py-2 text-green-700 text-sm flex items-center gap-2">
            <span>✓</span> {saveMessage}
          </div>
        )}
        {saveStatus === 'error' && (
          <div className="bg-red-50 border-b border-red-100 px-5 py-2 text-red-700 text-sm flex items-center gap-2">
            <span>✗</span> {saveMessage}
          </div>
        )}

        {/* Types list */}
        {types.length === 0 ? (
          <div className="text-center py-16 text-gray-400">
            <div className="text-4xl mb-3">📋</div>
            <p className="font-medium text-gray-500">No registration types added yet</p>
            <p className="text-sm mt-1">Click "Add Type" to add registration types manually.</p>
          </div>
        ) : (
          <ul className="divide-y divide-gray-50">
            {types.map((type) => {
              const isSelected = selectedIds.has(type.id);
              return (
                <li
                  key={type.id}
                  className={`flex items-center gap-4 px-5 py-4 hover:bg-gray-50 transition-colors cursor-pointer ${
                    isSelected ? 'bg-green-50' : ''
                  }`}
                  onClick={() => toggleSelect(type.id)}
                >
                  {/* Checkbox */}
                  <div
                    className={`w-5 h-5 rounded border-2 flex items-center justify-center flex-shrink-0 transition-colors ${
                      isSelected
                        ? 'bg-green-600 border-green-600'
                        : 'border-gray-300 bg-white'
                    }`}
                  >
                    {isSelected && (
                      <svg className="w-3 h-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    )}
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-gray-900">{type.name}</span>
                      <span className="text-xs font-mono bg-gray-100 text-gray-500 px-2 py-0.5 rounded">
                        {type.code}
                      </span>
                      {type.isFromEventsAir && (
                        <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded font-medium">
                          EventsAir
                        </span>
                      )}
                    </div>
                    {type.description && (
                      <p className="text-sm text-gray-500 mt-0.5 truncate">{type.description}</p>
                    )}
                  </div>

                  {/* Sync badge */}
                  <div className="flex items-center gap-3 flex-shrink-0">
                    {isSelected ? (
                      <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded-full font-medium">
                        ✓ Will Sync
                      </span>
                    ) : (
                      <span className="text-xs bg-gray-100 text-gray-400 px-2 py-1 rounded-full">
                        Not synced
                      </span>
                    )}

                    {/* Delete button */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        if (confirm(`Delete "${type.name}"?`)) {
                          deleteMutation.mutate(type.id);
                        }
                      }}
                      className="text-gray-300 hover:text-red-500 transition-colors p-1 rounded"
                      title="Delete"
                    >
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                      </svg>
                    </button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {/* Info box */}
      <div className="bg-blue-50 border border-blue-100 rounded-xl p-4 text-sm text-blue-800">
        <p className="font-semibold mb-1">ℹ️ How Sync Selection Works</p>
        <p>
          When EventsAir synchronisation runs, only guests whose registration type matches one of the
          selected types above will be imported into the Hospitality Platform. Guests with unselected
          registration types will be ignored. This allows you to focus on VIPs, ministers, and
          specific delegations while excluding general attendees.
        </p>
      </div>
    </div>
  );
}
