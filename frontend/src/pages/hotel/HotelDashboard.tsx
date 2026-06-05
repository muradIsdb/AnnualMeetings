import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';

// ─── Types ────────────────────────────────────────────────────────────────────

interface HotelGuestCountDto {
  hotelName: string;
  guestCount: number;
}

interface HotelRecentCheckinDto {
  id: string;
  fullName: string;
  initials: string;
  hotelName?: string;
  roomNumber?: string;
  checkedInAt?: string;
}

interface HotelNoRoomGuestDto {
  id: string;
  fullName: string;
  initials: string;
  hotelName?: string;
  checkedInAt?: string;
}

interface HotelSummaryData {
  totalAtHotel: number;
  enRouteToHotel: number;
  departingActive: number;
  noRoomAssigned: number;
  byHotel: HotelGuestCountDto[];
  outboundAtHotel: number;
  inTransferToAirport: number;
  atAirport: number;
  boardingCompleted: number;
  recentCheckins: HotelRecentCheckinDto[];
  guestsWithoutRoom: HotelNoRoomGuestDto[];
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function HotelDashboard() {
  const [data, setData] = useState<HotelSummaryData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const navigate = useNavigate();

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data: d } = await axios.get('/api/dashboard/hotel-summary');
      setData(d);
      setLastUpdated(new Date());
    } catch {
      setError('Failed to load hotel dashboard data. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    const id = setInterval(fetchData, 60_000);
    return () => clearInterval(id);
  }, [fetchData]);

  const timeAgo = (dateStr?: string) => {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 60) return `${diffMins} min ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours} hr${diffHours > 1 ? 's' : ''} ago`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  };

  const getHotelColor = (index: number) => {
    const colors = [
      { bg: 'bg-[#1a6b3c]/10', text: 'text-[#1a6b3c]' }, // isdb-green
      { bg: 'bg-blue-50', text: 'text-blue-600' },
      { bg: 'bg-purple-50', text: 'text-purple-600' },
      { bg: 'bg-amber-50', text: 'text-amber-600' },
      { bg: 'bg-rose-50', text: 'text-rose-600' },
      { bg: 'bg-cyan-50', text: 'text-cyan-600' },
      { bg: 'bg-gray-100', text: 'text-gray-600' }
    ];
    return colors[index % colors.length];
  };

  if (loading && !data) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#1a6b3c]" />
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6 bg-gray-50 min-h-[calc(100vh-64px)]">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-white border-b border-gray-200 px-6 py-4 -mx-6 -mt-6 sticky top-0 z-10 shadow-sm">
        <div>
          <h1 className="text-xl font-bold text-gray-900">Hotel Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Live occupancy & guest status across all hotels</p>
        </div>
        <div className="flex items-center gap-3">
          {lastUpdated && (
            <span className="text-xs text-gray-400 bg-gray-100 px-3 py-1.5 rounded-full hidden sm:inline-block">
              Updated: {lastUpdated.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </span>
          )}
          <span className="text-xs text-gray-400 bg-gray-100 px-3 py-1.5 rounded-full hidden sm:inline-block">Auto-refreshes every 60s</span>
          <button
            onClick={fetchData}
            className="px-2 py-1 rounded bg-gray-100 hover:bg-gray-200 text-gray-600 text-xs font-medium"
          >
            ↺ Refresh
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm">{error}</div>
      )}

      {data && (
        <>
          {/* SECTION 1: Summary KPI Cards */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {/* Total at Hotel */}
            <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Total at Hotel</span>
                <span className="text-2xl">🏨</span>
              </div>
              <p className="text-3xl font-bold text-gray-900">{data.totalAtHotel}</p>
              <p className="text-xs text-gray-400 mt-1">guests currently checked in</p>
            </div>

            {/* En Route to Hotel */}
            <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">En Route to Hotel</span>
                <span className="text-2xl">🚗</span>
              </div>
              <p className="text-3xl font-bold text-[#1a6b3c]">{data.enRouteToHotel}</p>
              <p className="text-xs text-gray-400 mt-1">vehicle assigned or received</p>
            </div>

            {/* Departing Today */}
            <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Departing Today</span>
                <span className="text-2xl">✈️</span>
              </div>
              <p className="text-3xl font-bold text-amber-600">{data.departingActive}</p>
              <p className="text-xs text-gray-400 mt-1">guests with outbound status</p>
            </div>

            {/* Without Room # */}
            <div className={`bg-white rounded-xl border ${data.noRoomAssigned > 0 ? 'border-red-200' : 'border-gray-200'} p-5 shadow-sm`}>
              <div className="flex items-center justify-between mb-3">
                <span className={`text-xs font-semibold ${data.noRoomAssigned > 0 ? 'text-red-500' : 'text-gray-500'} uppercase tracking-wide`}>
                  No Room Assigned
                </span>
                <span className="text-2xl">{data.noRoomAssigned > 0 ? '⚠️' : '✅'}</span>
              </div>
              <p className={`text-3xl font-bold ${data.noRoomAssigned > 0 ? 'text-red-500' : 'text-gray-900'}`}>{data.noRoomAssigned}</p>
              <p className="text-xs text-gray-400 mt-1">checked in without room #</p>
            </div>
          </div>

          {/* SECTION 2: Per-Hotel Guest Count */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h2 className="text-sm font-semibold text-gray-800">Guests by Hotel</h2>
              <span className="text-xs text-gray-400">{data.byHotel.length} active hotels</span>
            </div>
            <div className="divide-y divide-gray-50">
              {data.byHotel.length === 0 ? (
                <div className="px-5 py-8 text-center text-sm text-gray-500">No guests currently checked in to any hotel.</div>
              ) : (
                data.byHotel.map((h, i) => {
                  const color = getHotelColor(i);
                  return (
                    <div key={h.hotelName} className="px-5 py-4 flex items-center gap-4">
                      <div className={`w-8 h-8 rounded-lg ${color.bg} flex items-center justify-center flex-shrink-0`}>
                        <span className={`${color.text} font-bold text-xs`}>H{i + 1}</span>
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center justify-between">
                          <p className="text-sm font-semibold text-gray-900 truncate">{h.hotelName}</p>
                          <span className={`px-2 py-1 rounded-full text-[11px] font-semibold ${color.bg} ${color.text} ml-4 flex-shrink-0`}>
                            {h.guestCount} guest{h.guestCount !== 1 ? 's' : ''}
                          </span>
                        </div>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* SECTION 3: Departure Status + Recent Check-ins */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Departure Status Breakdown */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
              <div className="px-5 py-4 border-b border-gray-100">
                <h2 className="text-sm font-semibold text-gray-800">Departure Status</h2>
                <p className="text-xs text-gray-400 mt-0.5">Outbound journey progress for at-hotel guests</p>
              </div>
              <div className="p-5 space-y-3">
                <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
                  <div className="flex items-center gap-3">
                    <span className="w-2.5 h-2.5 rounded-full bg-gray-400 flex-shrink-0"></span>
                    <span className="text-sm text-gray-700">At Hotel (not yet departing)</span>
                  </div>
                  <span className="text-sm font-bold text-gray-900">{data.outboundAtHotel}</span>
                </div>
                <div className="flex items-center justify-between p-3 rounded-lg bg-amber-50">
                  <div className="flex items-center gap-3">
                    <span className="w-2.5 h-2.5 rounded-full bg-amber-500 flex-shrink-0"></span>
                    <span className="text-sm text-amber-800">In Transfer to Airport</span>
                  </div>
                  <span className="text-sm font-bold text-amber-700">{data.inTransferToAirport}</span>
                </div>
                <div className="flex items-center justify-between p-3 rounded-lg bg-blue-50">
                  <div className="flex items-center gap-3">
                    <span className="w-2.5 h-2.5 rounded-full bg-blue-500 flex-shrink-0"></span>
                    <span className="text-sm text-blue-800">At Airport</span>
                  </div>
                  <span className="text-sm font-bold text-blue-700">{data.atAirport}</span>
                </div>
                <div className="flex items-center justify-between p-3 rounded-lg bg-green-50">
                  <div className="flex items-center gap-3">
                    <span className="w-2.5 h-2.5 rounded-full bg-green-500 flex-shrink-0"></span>
                    <span className="text-sm text-green-800">Boarding Completed</span>
                  </div>
                  <span className="text-sm font-bold text-green-700">{data.boardingCompleted}</span>
                </div>
              </div>
            </div>

            {/* Recent Check-ins */}
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden flex flex-col">
              <div className="px-5 py-4 border-b border-gray-100 flex-shrink-0">
                <h2 className="text-sm font-semibold text-gray-800">Recent Check-ins</h2>
                <p className="text-xs text-gray-400 mt-0.5">Latest guests who checked in at hotel</p>
              </div>
              <div className="divide-y divide-gray-50 flex-1 overflow-y-auto max-h-[300px]">
                {data.recentCheckins.length === 0 ? (
                  <div className="px-5 py-8 text-center text-sm text-gray-500">No recent check-ins.</div>
                ) : (
                  data.recentCheckins.map((g, i) => {
                    const color = getHotelColor(i);
                    return (
                      <div key={g.id} className="px-5 py-3.5 flex items-center gap-3 hover:bg-gray-50 transition-colors cursor-pointer" onClick={() => navigate(`/guests/${g.id}`)}>
                        <div className={`w-8 h-8 rounded-full ${color.bg} flex items-center justify-center flex-shrink-0`}>
                          <span className={`${color.text} font-semibold text-xs`}>{g.initials}</span>
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-gray-900 truncate">{g.fullName}</p>
                          <p className="text-xs text-gray-400 truncate">
                            {g.hotelName || 'Unknown Hotel'} · {g.roomNumber ? `Room ${g.roomNumber}` : <span className="text-amber-500 font-medium">No room #</span>}
                          </p>
                        </div>
                        <span className="text-xs text-gray-400 flex-shrink-0">{timeAgo(g.checkedInAt)}</span>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
          </div>

          {/* SECTION 4: Guests Without Room Number */}
          {data.guestsWithoutRoom.length > 0 && (
            <div className="bg-white rounded-xl border border-red-100 shadow-sm overflow-hidden">
              <div className="px-5 py-4 border-b border-red-100 flex items-center justify-between bg-red-50/50">
                <div className="flex items-center gap-2">
                  <span className="text-red-500 text-base">⚠️</span>
                  <h2 className="text-sm font-semibold text-red-700">Guests Without Room Number</h2>
                  <span className="px-2 py-1 rounded-full text-[11px] font-semibold bg-red-100 text-red-700">
                    {data.guestsWithoutRoom.length} guest{data.guestsWithoutRoom.length !== 1 ? 's' : ''}
                  </span>
                </div>
              </div>
              <div className="divide-y divide-gray-50 max-h-[400px] overflow-y-auto">
                {data.guestsWithoutRoom.map((g) => (
                  <div key={g.id} className="px-5 py-3.5 flex items-center justify-between hover:bg-gray-50 transition-colors">
                    <div className="flex items-center gap-3 cursor-pointer" onClick={() => navigate(`/guests/${g.id}`)}>
                      <div className="w-7 h-7 rounded-full bg-gray-100 flex items-center justify-center flex-shrink-0">
                        <span className="text-gray-600 font-semibold text-xs">{g.initials}</span>
                      </div>
                      <div>
                        <p className="text-sm font-medium text-gray-900">{g.fullName}</p>
                        <p className="text-xs text-gray-400">
                          {g.hotelName || 'Unknown Hotel'} · Checked in {timeAgo(g.checkedInAt)}
                        </p>
                      </div>
                    </div>
                    <button
                      onClick={() => navigate('/hotel/guests')}
                      className="text-xs bg-[#1a6b3c] text-white px-3 py-1.5 rounded-lg font-medium hover:bg-[#155a30] transition-colors"
                    >
                      Assign Room
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
