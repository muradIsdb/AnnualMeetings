import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ReceptionGuestItem {
  id: string;
  fullName: string;
  designation?: string;
  nationality?: string;
  flightNumber?: string;
  airlineName?: string;
  scheduledArrival?: string;
  actualArrival?: string;
  inboundStatusValue: number;
  inboundStatusLabel: string;
  activeVehiclePlate?: string;
  isCritical: boolean;
  requiresAccessibility: boolean;
  flightCancelled: boolean;
  hasVehicle: boolean;
}

interface ReceptionFlightItem {
  flightId: string;
  flightNumber: string;
  airlineName?: string;
  departurePortName?: string;
  scheduledArrival: string;
  actualArrival?: string;
  flightStatus: string;
  actualTerminal?: string;
  actualGate?: string;
  liveDelayMinutes?: number;
  totalGuests: number;
  scheduled: number;
  arrivedAtAirport: number;
  receivedByEmbassy: number;
  inTransitToHotel: number;
}

interface AlertGuestItem {
  id: string;
  fullName: string;
  designation?: string;
  nationality?: string;
  flightNumber?: string;
  hasVehicle: boolean;
}

interface ReceptionSummaryData {
  totalArriving: number;
  scheduled: number;
  arrivedAtAirport: number;
  receivedByEmbassy: number;
  inTransitToHotel: number;
  criticalGuests: AlertGuestItem[];
  accessibilityGuests: AlertGuestItem[];
  flights: ReceptionFlightItem[];
  guests: ReceptionGuestItem[];
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

const INBOUND_STATUS_LABELS: Record<number, string> = {
  0: 'Scheduled',
  1: 'Arrived at Airport',
  2: 'Received by Embassy',
  3: 'In Transit → Hotel',
  4: 'At Hotel',
};

const INBOUND_STATUS_COLORS: Record<number, string> = {
  0: 'bg-gray-100 text-gray-700',
  1: 'bg-blue-100 text-blue-700',
  2: 'bg-amber-100 text-amber-700',
  3: 'bg-indigo-100 text-indigo-700',
  4: 'bg-emerald-100 text-emerald-700',
};

const FLIGHT_STATUS_COLORS: Record<string, string> = {
  Scheduled: 'bg-blue-100 text-blue-700',
  Active: 'bg-emerald-100 text-emerald-700',
  Landed: 'bg-green-100 text-green-700',
  Cancelled: 'bg-red-100 text-red-700',
  Diverted: 'bg-orange-100 text-orange-700',
  Unknown: 'bg-gray-100 text-gray-700',
};

const todayStr = () => new Date().toISOString().slice(0, 10);

// ─── Component ────────────────────────────────────────────────────────────────

export default function ReceptionDashboard() {
  const [fromDate, setFromDate] = useState(todayStr);
  const [toDate, setToDate] = useState(todayStr);
  const [appliedFrom, setAppliedFrom] = useState(todayStr);
  const [appliedTo, setAppliedTo] = useState(todayStr);
  const [data, setData] = useState<ReceptionSummaryData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const [guestSearch, setGuestSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const navigate = useNavigate();

  const fetchData = useCallback(async (from: string, to: string) => {
    setLoading(true);
    setError(null);
    try {
      const { data: d } = await axios.get('/api/dashboard/reception-summary', {
        params: { from, to },
      });
      setData(d);
      setLastUpdated(new Date());
    } catch {
      setError('Failed to load reception data. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData(appliedFrom, appliedTo);
  }, [appliedFrom, appliedTo, fetchData]);

  useEffect(() => {
    const id = setInterval(() => fetchData(appliedFrom, appliedTo), 60_000);
    return () => clearInterval(id);
  }, [appliedFrom, appliedTo, fetchData]);

  const applyFilter = () => {
    setAppliedFrom(fromDate);
    setAppliedTo(toDate);
  };

  const setQuick = (key: string) => {
    const d = new Date();
    let f = todayStr();
    let t = todayStr();
    if (key === 'yesterday') {
      d.setDate(d.getDate() - 1);
      f = t = d.toISOString().slice(0, 10);
    } else if (key === 'tomorrow') {
      d.setDate(d.getDate() + 1);
      f = t = d.toISOString().slice(0, 10);
    } else if (key === 'week') {
      const end = new Date();
      end.setDate(end.getDate() + 6);
      t = end.toISOString().slice(0, 10);
    } else if (key === '3days') {
      const end = new Date();
      end.setDate(end.getDate() + 2);
      t = end.toISOString().slice(0, 10);
    }
    setFromDate(f);
    setToDate(t);
    setAppliedFrom(f);
    setAppliedTo(t);
  };

  const filteredGuests = useMemo(() => {
    if (!data) return [];
    return data?.guests.filter((g) => {
      const matchSearch =
        !guestSearch ||
        g.fullName.toLowerCase().includes(guestSearch.toLowerCase()) ||
        (g.flightNumber?.toLowerCase().includes(guestSearch.toLowerCase()) ?? false);
      const matchStatus =
        !statusFilter || g.inboundStatusValue === parseInt(statusFilter, 10);
      return matchSearch && matchStatus;
    });
  }, [data, guestSearch, statusFilter]);

  if (loading && !data) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-isdb-green" />
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Reception Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Inbound guest arrival overview by date range</p>
        </div>
        <div className="flex items-center gap-2 text-xs text-gray-400">
          {lastUpdated && (
            <span>Last updated: {lastUpdated.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
          )}
          <span>· Auto-refreshes every 60s</span>
          <button
            onClick={() => fetchData(appliedFrom, appliedTo)}
            className="ml-1 px-2 py-1 rounded bg-gray-100 hover:bg-gray-200 text-gray-600 font-medium"
          >
            ↺ Refresh
          </button>
        </div>
      </div>

      {/* Date Filter Bar */}
      <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
        <div className="flex flex-wrap gap-2">
          {[
            ['yesterday', 'Yesterday'],
            ['today', 'Today'],
            ['tomorrow', 'Tomorrow'],
            ['week', 'This Week'],
            ['3days', 'Next 3 Days'],
          ].map(([key, label]) => (
            <button
              key={key}
              onClick={() => setQuick(key)}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors ${
                key === 'today' && appliedFrom === todayStr() && appliedTo === todayStr()
                  ? 'bg-isdb-green text-white border-isdb-green'
                  : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
              }`}
            >
              {label}
            </button>
          ))}
          <div className="flex items-center gap-2 ml-auto">
            <label className="text-xs text-gray-500">From</label>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-isdb-green"
            />
            <label className="text-xs text-gray-500">To</label>
            <input
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-isdb-green"
            />
            <button
              onClick={applyFilter}
              className="px-3 py-1.5 bg-isdb-green text-white rounded-lg text-xs font-medium hover:bg-isdb-green/90"
            >
              Apply
            </button>
          </div>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm">{error}</div>
      )}

      {data && (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
            <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Arriving</p>
              <p className="text-3xl font-bold text-gray-900 mt-1">{data.totalArriving}</p>
              <p className="text-xs text-gray-400 mt-1">in selected range</p>
            </div>
            <div className="bg-white rounded-xl border border-gray-200 p-4 shadow-sm">
              <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Scheduled</p>
              <p className="text-3xl font-bold text-gray-700 mt-1">{data?.scheduled}</p>
              <p className="text-xs text-gray-400 mt-1">not yet arrived</p>
            </div>
            <div className="bg-blue-50 rounded-xl border border-blue-100 p-4 shadow-sm">
              <p className="text-xs font-medium text-blue-600 uppercase tracking-wide">Arrived at Airport</p>
              <p className="text-3xl font-bold text-blue-700 mt-1">{data?.arrivedAtAirport}</p>
              <p className="text-xs text-blue-400 mt-1">flight landed</p>
            </div>
            <div className="bg-amber-50 rounded-xl border border-amber-100 p-4 shadow-sm">
              <p className="text-xs font-medium text-amber-600 uppercase tracking-wide">Received by Embassy</p>
              <p className="text-3xl font-bold text-amber-700 mt-1">{data?.receivedByEmbassy}</p>
              <p className="text-xs text-amber-400 mt-1">embassy team received</p>
            </div>
            <div className="bg-indigo-50 rounded-xl border border-indigo-100 p-4 shadow-sm">
              <p className="text-xs font-medium text-indigo-600 uppercase tracking-wide">In Transit → Hotel</p>
              <p className="text-3xl font-bold text-indigo-700 mt-1">{data?.inTransitToHotel}</p>
              <p className="text-xs text-indigo-400 mt-1">vehicle assigned</p>
            </div>
          </div>

          {/* Alerts */}
          {(data?.criticalGuests.length > 0 || data?.accessibilityGuests.length > 0) && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
              <div className="px-5 py-3 border-b border-gray-100">
                <h2 className="text-sm font-semibold text-gray-900">Alerts</h2>
              </div>
              <div className="p-4 space-y-2">
                {data?.criticalGuests.map((g) => (
                  <div key={g.id} className="flex items-center justify-between bg-red-50 border border-red-100 rounded-lg px-4 py-2.5">
                    <div className="flex items-center gap-3">
                      <span className="w-2 h-2 rounded-full bg-red-500 flex-shrink-0" />
                      <div>
                        <p className="text-sm font-medium text-gray-900">{g.fullName}</p>
                        <p className="text-xs text-gray-500">
                          {[g.designation, g.nationality, g.flightNumber].filter(Boolean).join(' · ')}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full font-medium">Critical</span>
                      {!g.hasVehicle && (
                        <span className="text-xs bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full font-medium">No Vehicle</span>
                      )}
                      <button onClick={() => navigate(`/airport/guest/${g.id}`)} className="text-xs text-isdb-green hover:underline font-medium">View</button>
                    </div>
                  </div>
                ))}
                {data?.accessibilityGuests.map((g) => (
                  <div key={g.id} className="flex items-center justify-between bg-amber-50 border border-amber-100 rounded-lg px-4 py-2.5">
                    <div className="flex items-center gap-3">
                      <span className="w-2 h-2 rounded-full bg-amber-500 flex-shrink-0" />
                      <div>
                        <p className="text-sm font-medium text-gray-900">{g.fullName}</p>
                        <p className="text-xs text-gray-500">
                          {[g.designation, g.nationality, g.flightNumber].filter(Boolean).join(' · ')}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full font-medium">Accessibility</span>
                      {!g.hasVehicle && (
                        <span className="text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full font-medium">No Vehicle</span>
                      )}
                      <button onClick={() => navigate(`/airport/guest/${g.id}`)} className="text-xs text-isdb-green hover:underline font-medium">View</button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Flights Timeline */}
          {data?.flights.length > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
              <div className="px-5 py-3 border-b border-gray-100">
                <h2 className="text-sm font-semibold text-gray-900">Flights Timeline</h2>
              </div>
              <div className="divide-y divide-gray-50">
                {data?.flights.map((f) => (
                  <div key={f.flightId} className={`px-5 py-3 ${f.flightStatus === 'Cancelled' ? 'opacity-60' : ''}`}>
                    <div className="flex flex-wrap items-center gap-3">
                      <span className="text-sm font-bold text-gray-900">
                        {new Date(f.scheduledArrival).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                      </span>
                      <span className={`text-sm font-semibold ${f.flightStatus === 'Cancelled' ? 'line-through text-gray-400' : 'text-gray-800'}`}>
                        {f.flightNumber}
                      </span>
                      {f.airlineName && <span className="text-xs text-gray-500">{f.airlineName}</span>}
                      {f.departurePortName && <span className="text-xs text-gray-400">from {f.departurePortName}</span>}
                      {f.actualTerminal && <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded">T{f.actualTerminal}</span>}
                      {f.actualGate && <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded">Gate {f.actualGate}</span>}
                      {f.actualArrival && (
                        <span className="text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded font-medium">
                          Actual: {new Date(f.actualArrival).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </span>
                      )}
                      <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${FLIGHT_STATUS_COLORS[f.flightStatus] ?? 'bg-gray-100 text-gray-700'}`}>
                        {f.flightStatus}
                      </span>
                      {f.liveDelayMinutes != null && f.liveDelayMinutes > 0 && (
                        <span className="text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full font-medium">+{f.liveDelayMinutes}min delay</span>
                      )}
                      <span className="text-xs font-medium text-gray-600 ml-auto">
                        {f.totalGuests} guest{f.totalGuests !== 1 ? 's' : ''}
                      </span>
                    </div>
                    <div className="flex gap-2 mt-2">
                      {f.scheduled > 0 && <span className="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">{f.scheduled} Scheduled</span>}
                      {f.arrivedAtAirport > 0 && <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">{f.arrivedAtAirport} Arrived</span>}
                      {f.receivedByEmbassy > 0 && <span className="text-xs bg-amber-100 text-amber-700 px-2 py-0.5 rounded-full">{f.receivedByEmbassy} Received</span>}
                      {f.inTransitToHotel > 0 && <span className="text-xs bg-indigo-100 text-indigo-700 px-2 py-0.5 rounded-full">{f.inTransitToHotel} In Transit</span>}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Guest List Table */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
            <div className="px-5 py-3 border-b border-gray-100 flex flex-wrap items-center gap-3">
              <h2 className="text-sm font-semibold text-gray-900">Guest List</h2>
              <span className="text-xs text-gray-400">({filteredGuests.length} of {data?.guests.length})</span>
              <input
                type="text"
                placeholder="Search by name or flight..."
                value={guestSearch}
                onChange={(e) => setGuestSearch(e.target.value)}
                className="ml-auto border border-gray-200 rounded-lg px-3 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-isdb-green w-48"
              />
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="border border-gray-200 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-isdb-green"
              >
                <option value="">All Statuses</option>
                {Object.entries(INBOUND_STATUS_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="bg-gray-50 text-xs font-semibold text-gray-500 uppercase tracking-wide">
                    <th className="px-4 py-3 text-left">Guest</th>
                    <th className="px-4 py-3 text-left">Flight</th>
                    <th className="px-4 py-3 text-left">Arrival Time</th>
                    <th className="px-4 py-3 text-left">Status</th>
                    <th className="px-4 py-3 text-left">Vehicle</th>
                    <th className="px-4 py-3 text-left">Flags</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {filteredGuests.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="px-4 py-8 text-center text-gray-400 text-sm">
                        {data?.guests.length === 0 ? 'No guests arriving in this date range' : 'No guests match the current filters'}
                      </td>
                    </tr>
                  ) : (
                    filteredGuests.map((g) => (
                      <tr key={g.id} className="hover:bg-gray-50 cursor-pointer" onClick={() => navigate(`/airport/guest/${g.id}`)}>
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-900">{g.fullName}</p>
                          <p className="text-xs text-gray-400">{[g.designation, g.nationality].filter(Boolean).join(' · ')}</p>
                        </td>
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-800">{g.flightNumber ?? '—'}</p>
                          <p className="text-xs text-gray-400">{g.airlineName ?? ''}</p>
                        </td>
                        <td className="px-4 py-3">
                          <div className="space-y-0.5">
                            <div className="text-gray-600 text-sm">
                              {g.scheduledArrival
                                ? new Date(g.scheduledArrival).toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
                                : '—'}
                            </div>
                            {g.actualArrival && (
                              <div className="text-xs text-green-700 font-medium">
                                Actual: {new Date(g.actualArrival).toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                              </div>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${INBOUND_STATUS_COLORS[g.inboundStatusValue] ?? 'bg-gray-100 text-gray-700'}`}>
                            {g.inboundStatusLabel}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-gray-600 font-mono text-xs">
                          {g.activeVehiclePlate ?? <span className="text-gray-300">—</span>}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex gap-1 flex-wrap">
                            {g.isCritical && <span className="text-xs bg-red-100 text-red-700 px-1.5 py-0.5 rounded font-medium">Critical</span>}
                            {g.requiresAccessibility && <span className="text-xs bg-amber-100 text-amber-700 px-1.5 py-0.5 rounded font-medium">Access.</span>}
                            {g.flightCancelled && <span className="text-xs bg-red-100 text-red-700 px-1.5 py-0.5 rounded font-medium">Cancelled</span>}
                            {!g.activeVehiclePlate && g.inboundStatusValue < 3 && (
                              <span className="text-xs bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded font-medium">No Vehicle</span>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
