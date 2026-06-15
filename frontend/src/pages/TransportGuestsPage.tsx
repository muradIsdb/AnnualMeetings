import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Search, Users, User } from 'lucide-react';
import { guestsApi } from '../api/services';

/**
 * Transport Guests Page
 * Similar to Airport page but:
 * - No top summary cards (expected, arrived, vehicle assigned, in hotel)
 * - No placard button
 * - Shows Deserved Car Class and Old Hotel columns
 * Accessible by Admin and Transport roles
 */
export default function TransportGuestsPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [carClassFilter, setCarClassFilter] = useState('all');

  const { data: guests = [], isLoading } = useQuery({
    queryKey: ['guests', 'transport-guests'],
    queryFn: () => guestsApi.getAll(),
    refetchInterval: 30000,
  });

  const carClasses = useMemo(() => {
    const classes = new Set<string>();
    guests.forEach((g: any) => {
      if (g.deservedCarClassName) classes.add(g.deservedCarClassName);
    });
    return Array.from(classes).sort();
  }, [guests]);

  const statuses = useMemo(() => {
    const s = new Set<string>();
    guests.forEach((g: any) => {
      if (g.inboundStatusLabel) s.add(g.inboundStatusLabel);
    });
    return Array.from(s).sort();
  }, [guests]);

  const filtered = useMemo(() => {
    return guests.filter((g: any) => {
      if (search) {
        const q = search.toLowerCase();
        const match =
          (g.fullName || '').toLowerCase().includes(q) ||
          (g.designation || '').toLowerCase().includes(q) ||
          (g.registrationTypeName || '').toLowerCase().includes(q) ||
          (g.country || '').toLowerCase().includes(q) ||
          (g.organization || '').toLowerCase().includes(q) ||
          (g.deservedCarClassName || '').toLowerCase().includes(q) ||
          (g.oldHotel || '').toLowerCase().includes(q) ||
          (g.hotelName || '').toLowerCase().includes(q);
        if (!match) return false;
      }
      if (statusFilter !== 'all' && g.inboundStatusLabel !== statusFilter) return false;
      if (carClassFilter !== 'all' && g.deservedCarClassName !== carClassFilter) return false;
      return true;
    });
  }, [guests, search, statusFilter, carClassFilter]);

  const formatStatus = (status: string) => {
    if (!status) return '—';
    return status.replace(/([A-Z])/g, ' $1').trim();
  };

  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case 'Arrived':
        return 'bg-blue-100 text-blue-700';
      case 'VehicleAssigned':
        return 'bg-green-100 text-green-700';
      case 'AtHotel':
        return 'bg-indigo-100 text-indigo-700';
      case 'LeftHotel':
        return 'bg-gray-100 text-gray-700';
      case 'ReceivedByEmbassyTeam':
        return 'bg-purple-100 text-purple-700';
      default:
        return 'bg-gray-100 text-gray-600';
    }
  };

  return (
    <div className="p-4 md:p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center">
            <Users className="w-5 h-5 text-emerald-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Transport Guests</h1>
            <p className="text-sm text-gray-500">Guest directory for transport operations</p>
          </div>
        </div>
        <span className="text-sm text-gray-500">
          {filtered.length} of {guests.length} guests
        </span>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by name, designation, car class, hotel…"
            className="w-full pl-10 pr-4 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green bg-white"
        >
          <option value="all">All Statuses</option>
          {statuses.map((s) => (
            <option key={s} value={s}>
              {formatStatus(s)}
            </option>
          ))}
        </select>
        <select
          value={carClassFilter}
          onChange={(e) => setCarClassFilter(e.target.value)}
          className="px-3 py-2.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-isdb-green bg-white"
        >
          <option value="all">All Car Classes</option>
          {carClasses.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>

      {/* Guest Table */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden shadow-sm">
        {isLoading && guests.length === 0 ? (
          <div className="p-8 text-center">
            <div className="animate-pulse space-y-3">
              {[1, 2, 3, 4, 5].map((i) => (
                <div key={i} className="h-14 bg-gray-100 rounded-lg" />
              ))}
            </div>
          </div>
        ) : filtered.length === 0 ? (
          <div className="p-12 text-center">
            <Users className="w-12 h-12 text-gray-300 mx-auto mb-3" />
            <p className="text-gray-500">No guests match your filters.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="text-left py-3 px-4 font-medium text-gray-600">Guest</th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600 hidden md:table-cell">
                    Registration Type
                  </th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600">
                    Deserved Car Class
                  </th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600 hidden lg:table-cell">
                    Old Hotel
                  </th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600 hidden lg:table-cell">
                    Current Hotel
                  </th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600 hidden md:table-cell">
                    Vehicle
                  </th>
                  <th className="text-left py-3 px-4 font-medium text-gray-600">Status</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((g: any) => (
                  <tr
                    key={g.id}
                    onClick={() => navigate(`/airport/guest/${g.id}`)}
                    className={`border-b border-gray-100 hover:bg-gray-50 cursor-pointer transition-colors ${
                      g.isCritical ? 'border-l-4 border-l-red-500' : ''
                    }`}
                  >
                    <td className="py-3 px-4">
                      <div className="flex items-center gap-3">
                        {g.photoUrl ? (
                          <img
                            src={g.photoUrl}
                            alt={g.fullName}
                            className="w-9 h-9 rounded-full object-cover flex-shrink-0"
                          />
                        ) : (
                          <div className="w-9 h-9 rounded-full bg-isdb-green/10 flex items-center justify-center flex-shrink-0">
                            <User className="w-4 h-4 text-isdb-green" />
                          </div>
                        )}
                        <div className="min-w-0">
                          <p className="font-medium text-gray-900 truncate">{g.fullName}</p>
                          <p className="text-xs text-gray-500 truncate">
                            {[g.designation, g.country].filter(Boolean).join(' · ') || '—'}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="py-3 px-4 hidden md:table-cell">
                      {g.registrationTypeName ? (
                        <span className="inline-flex items-center text-xs font-medium text-indigo-700 bg-indigo-50 border border-indigo-100 px-2 py-0.5 rounded-full">
                          {g.registrationTypeName}
                        </span>
                      ) : (
                        <span className="text-gray-400">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4">
                      {g.deservedCarClassName ? (
                        <span
                          className="inline-flex items-center text-xs font-semibold px-2.5 py-1 rounded-full"
                          style={{
                            backgroundColor: (g.deservedCarClassColor || '#6b7280') + '20',
                            color: g.deservedCarClassColor || '#6b7280',
                            border: `1px solid ${g.deservedCarClassColor || '#6b7280'}40`,
                          }}
                        >
                          {g.deservedCarClassName}
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">Not assigned</span>
                      )}
                    </td>
                    <td className="py-3 px-4 hidden lg:table-cell">
                      {g.oldHotel ? (
                        <span className="text-xs text-amber-700 bg-amber-50 border border-amber-100 px-2 py-0.5 rounded-full">
                          {g.oldHotel}
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4 hidden lg:table-cell">
                      {g.hotelName ? (
                        <span className="text-xs text-gray-700">
                          {g.hotelName}
                          {g.roomNumber ? ` · Rm ${g.roomNumber}` : ''}
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4 hidden md:table-cell">
                      {g.activeVehiclePlate ? (
                        <span className="inline-flex items-center text-xs font-medium text-green-700 bg-green-50 border border-green-100 px-2 py-0.5 rounded-full">
                          {g.activeVehiclePlate}
                        </span>
                      ) : (
                        <span className="text-gray-400 text-xs">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4">
                      <span
                        className={`text-xs px-2 py-0.5 rounded-full whitespace-nowrap ${getStatusBadgeClass(
                          g.inboundStatusLabel
                        )}`}
                      >
                        {formatStatus(g.inboundStatusLabel || g.statusLabel)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
