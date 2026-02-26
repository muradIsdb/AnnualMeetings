import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Trash2, AlertTriangle, RefreshCw, UserX } from 'lucide-react';
import apiClient from '../../api/client';

interface InactiveGuest {
  id: string;
  firstName: string;
  lastName: string;
  registrationTypeName?: string;
  organization?: string;
  email?: string;
  lastSyncedAt?: string;
  deactivatedAt?: string;
}

export default function InactiveParticipantsPage() {
  const qc = useQueryClient();
  const [showDeleteAllConfirm, setShowDeleteAllConfirm] = useState(false);
  const [deleteOneConfirm, setDeleteOneConfirm] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const { data: guests = [], isLoading, refetch } = useQuery<InactiveGuest[]>({
    queryKey: ['inactive-guests'],
    queryFn: () => apiClient.get('/guests/inactive').then(r => r.data),
  });

  const showFeedback = (type: 'success' | 'error', message: string) => {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 5000);
  };

  const deleteOneMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/guests/${id}`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['inactive-guests'] });
      setDeleteOneConfirm(null);
      showFeedback('success', 'Participant permanently deleted.');
    },
    onError: () => showFeedback('error', 'Failed to delete participant.'),
  });

  const deleteAllMutation = useMutation({
    mutationFn: () => apiClient.delete('/guests/inactive/all'),
    onSuccess: (res: any) => {
      qc.invalidateQueries({ queryKey: ['inactive-guests'] });
      setShowDeleteAllConfirm(false);
      showFeedback('success', res.data?.message || `All inactive participants deleted.`);
    },
    onError: () => showFeedback('error', 'Failed to delete inactive participants.'),
  });

  const formatDate = (dateStr?: string) => {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  };

  return (
    <div className="p-6 max-w-5xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Inactive Participants</h1>
          <p className="mt-1 text-sm text-gray-500">
            Participants whose registration type was deselected from the sync filters.
            They are hidden from all operational views but their data is preserved here.
            Re-selecting their registration type and syncing will restore them automatically.
          </p>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center gap-1.5 rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-600 hover:bg-gray-50"
        >
          <RefreshCw className="h-4 w-4" />
          Refresh
        </button>
      </div>

      {/* Feedback */}
      {feedback && (
        <div className={`flex items-center gap-2 rounded-lg p-3 text-sm font-medium ${
          feedback.type === 'success'
            ? 'bg-green-50 border border-green-200 text-green-800'
            : 'bg-red-50 border border-red-200 text-red-700'
        }`}>
          {feedback.message}
        </div>
      )}

      {/* Delete All confirmation */}
      {showDeleteAllConfirm && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4">
          <div className="flex items-start gap-3">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-red-500" />
            <div className="flex-1">
              <p className="font-semibold text-red-800">Permanently delete all {guests.length} inactive participant(s)?</p>
              <p className="mt-1 text-sm text-red-700">
                This action cannot be undone. All data for these participants will be permanently removed from the system.
              </p>
              <div className="mt-3 flex gap-2">
                <button
                  onClick={() => deleteAllMutation.mutate()}
                  disabled={deleteAllMutation.isPending}
                  className="rounded bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
                >
                  {deleteAllMutation.isPending ? 'Deleting…' : `Yes, Delete All ${guests.length}`}
                </button>
                <button
                  onClick={() => setShowDeleteAllConfirm(false)}
                  className="rounded border border-gray-300 px-4 py-2 text-sm text-gray-600 hover:bg-gray-50"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {isLoading ? (
        <div className="flex items-center justify-center h-48">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-gray-400" />
        </div>
      ) : guests.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 p-16 text-center">
          <UserX className="mx-auto h-12 w-12 text-gray-300 mb-3" />
          <p className="font-medium text-gray-500">No inactive participants</p>
          <p className="mt-1 text-sm text-gray-400">
            Participants become inactive when their registration type is deselected from the sync filters.
          </p>
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
          {/* Table header with delete all */}
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100 bg-gray-50">
            <span className="text-sm font-medium text-gray-700">
              <span className="text-red-600 font-bold">{guests.length}</span> inactive participant(s)
            </span>
            {!showDeleteAllConfirm && (
              <button
                onClick={() => setShowDeleteAllConfirm(true)}
                className="flex items-center gap-1.5 rounded-lg border border-red-300 bg-red-50 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-100"
              >
                <Trash2 className="h-4 w-4" />
                Delete All Inactive
              </button>
            )}
          </div>

          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr>
                <th className="px-5 py-3 text-left font-medium text-gray-600">Name</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">Registration Type</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">Organization</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">Last Synced</th>
                <th className="w-24 px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {guests.map(guest => (
                <tr key={guest.id} className="hover:bg-gray-50">
                  <td className="px-5 py-3">
                    <div className="font-medium text-gray-900">{guest.firstName} {guest.lastName}</div>
                    {guest.email && <div className="text-xs text-gray-400">{guest.email}</div>}
                  </td>
                  <td className="px-4 py-3">
                    {guest.registrationTypeName
                      ? <span className="rounded-full bg-orange-100 px-2 py-0.5 text-xs font-medium text-orange-700">{guest.registrationTypeName}</span>
                      : <span className="text-gray-400">—</span>}
                  </td>
                  <td className="px-4 py-3 text-gray-600">{guest.organization || '—'}</td>
                  <td className="px-4 py-3 text-gray-500 text-xs">{formatDate(guest.lastSyncedAt)}</td>
                  <td className="px-4 py-3">
                    {deleteOneConfirm === guest.id ? (
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => deleteOneMutation.mutate(guest.id)}
                          className="rounded bg-red-600 px-2 py-0.5 text-xs text-white hover:bg-red-700"
                        >
                          Confirm
                        </button>
                        <button
                          onClick={() => setDeleteOneConfirm(null)}
                          className="rounded border border-gray-300 px-2 py-0.5 text-xs text-gray-600 hover:bg-gray-50"
                        >
                          Cancel
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setDeleteOneConfirm(guest.id)}
                        className="rounded p-1 text-gray-300 hover:bg-red-50 hover:text-red-500"
                        title="Delete permanently"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
