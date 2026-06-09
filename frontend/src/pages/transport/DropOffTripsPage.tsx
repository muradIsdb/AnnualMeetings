import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../../api/client';
import { Car, CheckCircle, Clock, MapPin, User, Phone, Hash, Calendar } from 'lucide-react';

interface DropOffTrip {
  id: string;
  guestName: string;
  guestCountry?: string;
  carNumber?: string;
  vehicleMake?: string;
  vehicleModel?: string;
  licensePlate?: string;
  driverName?: string;
  driverPhone?: string;
  destination: string;
  notes?: string;
  loggedAt: string;
  completedAt?: string;
  status: 'InProgress' | 'Completed';
  loggedBy?: string;
  loggedByRole?: string;
}

interface DropOffTripsResponse {
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  items: DropOffTrip[];
}

const fetchDropOffTrips = async (status: string, page: number): Promise<DropOffTripsResponse> => {
  const res = await apiClient.get(`/fleet/dropoff-trips?status=${status}&page=${page}&pageSize=20`);
  return res.data;
};

const completeTrip = async (id: string) => {
  const res = await apiClient.post(`/fleet/dropoff-trips/${id}/complete`);
  return res.data;
};

const formatDate = (iso: string) => {
  const d = new Date(iso);
  return d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
};

export default function DropOffTripsPage() {
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const queryClient = useQueryClient();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['dropoff-trips', statusFilter, page],
    queryFn: () => fetchDropOffTrips(statusFilter, page),
    refetchInterval: 60000,
  });

  const completeMutation = useMutation({
    mutationFn: completeTrip,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dropoff-trips'] }),
  });

  const inProgressCount = data?.items.filter(t => t.status === 'InProgress').length ?? 0;
  const completedCount  = data?.items.filter(t => t.status === 'Completed').length ?? 0;

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Drop-off Trips</h1>
          <p className="text-sm text-gray-500 mt-1">Track all logged drop-off trips</p>
        </div>
        <span className="text-xs text-gray-400">Auto-refreshes every 60s</span>
      </div>

      {/* KPI Strip */}
      <div className="grid grid-cols-3 gap-4">
        <div className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-blue-100 flex items-center justify-center">
            <Car className="w-5 h-5 text-blue-600" />
          </div>
          <div>
            <div className="text-2xl font-bold text-gray-900">{data?.total ?? '—'}</div>
            <div className="text-xs text-gray-500">Total Trips</div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center">
            <Clock className="w-5 h-5 text-amber-600" />
          </div>
          <div>
            <div className="text-2xl font-bold text-amber-600">{inProgressCount}</div>
            <div className="text-xs text-gray-500">In Progress</div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-full bg-green-100 flex items-center justify-center">
            <CheckCircle className="w-5 h-5 text-green-600" />
          </div>
          <div>
            <div className="text-2xl font-bold text-green-600">{completedCount}</div>
            <div className="text-xs text-gray-500">Completed</div>
          </div>
        </div>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-2">
        {(['all', 'inprogress', 'completed'] as const).map(s => (
          <button
            key={s}
            onClick={() => { setStatusFilter(s); setPage(1); }}
            className={`px-4 py-1.5 rounded-full text-sm font-medium transition-colors ${
              statusFilter === s
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}
          >
            {s === 'all' ? 'All' : s === 'inprogress' ? 'In Progress' : 'Completed'}
          </button>
        ))}
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-gray-400">Loading trips...</div>
        ) : isError ? (
          <div className="p-12 text-center text-red-500">Failed to load trips.</div>
        ) : !data?.items.length ? (
          <div className="p-12 text-center text-gray-400">No drop-off trips found.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Guest</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Vehicle</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Driver</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Destination</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Date & Time</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Logged By</th>
                <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wide">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data.items.map(trip => (
                <tr key={trip.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{trip.guestName}</div>
                    {trip.guestCountry && (
                      <div className="text-xs text-gray-400">{trip.guestCountry}</div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 text-gray-700">
                      <Hash className="w-3 h-3 text-gray-400" />
                      <span className="font-mono font-medium">{trip.carNumber ?? '—'}</span>
                    </div>
                    {trip.vehicleMake && (
                      <div className="text-xs text-gray-400">{trip.vehicleMake} {trip.vehicleModel}</div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 text-gray-700">
                      <User className="w-3 h-3 text-gray-400" />
                      <span>{trip.driverName ?? '—'}</span>
                    </div>
                    {trip.driverPhone && (
                      <div className="flex items-center gap-1 text-xs text-gray-400">
                        <Phone className="w-3 h-3" />
                        {trip.driverPhone}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 text-gray-700">
                      <MapPin className="w-3 h-3 text-gray-400" />
                      <span>{trip.destination}</span>
                    </div>
                    {trip.notes && (
                      <div className="text-xs text-gray-400 mt-0.5 italic">{trip.notes}</div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 text-gray-700">
                      <Calendar className="w-3 h-3 text-gray-400" />
                      <span>{formatDate(trip.loggedAt)}</span>
                    </div>
                    {trip.completedAt && (
                      <div className="text-xs text-green-600 mt-0.5">
                        Completed: {formatDate(trip.completedAt)}
                      </div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium ${
                      trip.status === 'Completed'
                        ? 'bg-green-100 text-green-700'
                        : 'bg-amber-100 text-amber-700'
                    }`}>
                      {trip.status === 'Completed'
                        ? <CheckCircle className="w-3 h-3" />
                        : <Clock className="w-3 h-3" />
                      }
                      {trip.status === 'Completed' ? 'Completed' : 'In Progress'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="text-gray-700">{trip.loggedBy ?? '—'}</div>
                    {trip.loggedByRole && (
                      <div className="text-xs text-gray-400">{trip.loggedByRole}</div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    {trip.status === 'InProgress' && (
                      <button
                        onClick={() => completeMutation.mutate(trip.id)}
                        disabled={completeMutation.isPending}
                        className="px-3 py-1.5 bg-green-600 text-white text-xs font-medium rounded-lg hover:bg-green-700 disabled:opacity-50 transition-colors"
                      >
                        Mark Complete
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-gray-500">
          <span>Showing page {data.page} of {data.totalPages} ({data.total} trips)</span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1.5 border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors"
            >
              Previous
            </button>
            <button
              onClick={() => setPage(p => Math.min(data.totalPages, p + 1))}
              disabled={page === data.totalPages}
              className="px-3 py-1.5 border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition-colors"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
