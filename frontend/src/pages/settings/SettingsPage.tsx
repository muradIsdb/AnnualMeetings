import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getAllHotels, createHotel, updateHotel, deleteHotel,
  getAllPickupDays, createPickupDay, updatePickupDay, deletePickupDay,
  getAllPickupHours, createPickupHour, updatePickupHour, deletePickupHour,
  HotelOption, PickupDayOption, PickupHourOption,
} from '../../api/settingsService';

type Tab = 'hotels' | 'days' | 'hours';

export default function SettingsPage() {
  const [tab, setTab] = useState<Tab>('hotels');

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Platform Settings</h1>
        <p className="text-gray-500 text-sm mt-1">
          Manage the dropdown options available in the Departure Request form.
        </p>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 mb-6 bg-gray-100 p-1 rounded-lg w-fit">
        {(['hotels', 'days', 'hours'] as Tab[]).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
              tab === t ? 'bg-white shadow text-green-700' : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            {t === 'hotels' ? '🏨 Hotels' : t === 'days' ? '📅 Pickup Days' : '🕐 Pickup Hours'}
          </button>
        ))}
      </div>

      {tab === 'hotels' && <HotelsPanel />}
      {tab === 'days' && <PickupDaysPanel />}
      {tab === 'hours' && <PickupHoursPanel />}
    </div>
  );
}

// ─── Hotels Panel ─────────────────────────────────────────────────────────────

function HotelsPanel() {
  const qc = useQueryClient();
  const { data: hotels = [], isLoading } = useQuery({ queryKey: ['hotels-all'], queryFn: getAllHotels });
  const [form, setForm] = useState({ name: '', isActive: true, displayOrder: 0 });
  const [editId, setEditId] = useState<string | null>(null);
  const [error, setError] = useState('');

  const createMut = useMutation({
    mutationFn: createHotel,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['hotels-all'] }); resetForm(); },
  });
  const updateMut = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Omit<HotelOption, 'id'> }) => updateHotel(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['hotels-all'] }); resetForm(); },
  });
  const deleteMut = useMutation({
    mutationFn: deleteHotel,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['hotels-all'] }),
  });

  const resetForm = () => { setForm({ name: '', isActive: true, displayOrder: 0 }); setEditId(null); setError(''); };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.name.trim()) { setError('Hotel name is required.'); return; }
    if (editId) updateMut.mutate({ id: editId, data: form });
    else createMut.mutate(form);
  };

  const startEdit = (h: HotelOption) => {
    setEditId(h.id);
    setForm({ name: h.name, isActive: h.isActive, displayOrder: h.displayOrder });
    setError('');
  };

  return (
    <div className="space-y-6">
      {/* Add / Edit Form */}
      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h2 className="text-base font-semibold text-gray-800 mb-4">
          {editId ? '✏️ Edit Hotel' : '➕ Add Hotel'}
        </h2>
        <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-medium text-gray-600 mb-1">Hotel Name *</label>
            <input
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="e.g. Hilton Jeddah"
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
            />
          </div>
          <div className="w-28">
            <label className="block text-xs font-medium text-gray-600 mb-1">Order</label>
            <input
              type="number" min={0}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              value={form.displayOrder}
              onChange={e => setForm(f => ({ ...f, displayOrder: parseInt(e.target.value) || 0 }))}
            />
          </div>
          <div className="flex items-center gap-2 pb-1">
            <input type="checkbox" id="hotel-active" checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
              className="w-4 h-4 accent-green-600" />
            <label htmlFor="hotel-active" className="text-sm text-gray-600">Active</label>
          </div>
          <button type="submit"
            disabled={createMut.isPending || updateMut.isPending}
            className="bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-green-800 disabled:opacity-50">
            {editId ? 'Update' : 'Add Hotel'}
          </button>
          {editId && (
            <button type="button" onClick={resetForm}
              className="bg-gray-200 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-300">
              Cancel
            </button>
          )}
        </form>
        {error && <p className="text-red-500 text-xs mt-2">{error}</p>}
      </div>

      {/* List */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <span className="text-sm font-semibold text-gray-700">Hotels ({hotels.length})</span>
        </div>
        {isLoading ? (
          <div className="p-6 text-center text-gray-400 text-sm">Loading...</div>
        ) : hotels.length === 0 ? (
          <div className="p-6 text-center text-gray-400 text-sm">No hotels added yet.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase">
              <tr>
                <th className="px-4 py-2 text-left">Name</th>
                <th className="px-4 py-2 text-center">Order</th>
                <th className="px-4 py-2 text-center">Status</th>
                <th className="px-4 py-2 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {hotels.map(h => (
                <tr key={h.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{h.name}</td>
                  <td className="px-4 py-3 text-center text-gray-500">{h.displayOrder}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${h.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                      {h.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right space-x-2">
                    <button onClick={() => startEdit(h)}
                      className="text-blue-600 hover:text-blue-800 text-xs font-medium">Edit</button>
                    <button onClick={() => { if (confirm(`Delete "${h.name}"?`)) deleteMut.mutate(h.id); }}
                      className="text-red-500 hover:text-red-700 text-xs font-medium">Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

// ─── Pickup Days Panel ────────────────────────────────────────────────────────

function PickupDaysPanel() {
  const qc = useQueryClient();
  const { data: days = [], isLoading } = useQuery({ queryKey: ['pickup-days-all'], queryFn: getAllPickupDays });
  const [form, setForm] = useState({ label: '', value: '', isActive: true, displayOrder: 0 });
  const [editId, setEditId] = useState<string | null>(null);
  const [error, setError] = useState('');

  const createMut = useMutation({
    mutationFn: createPickupDay,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pickup-days-all'] }); resetForm(); },
  });
  const updateMut = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Omit<PickupDayOption, 'id'> }) => updatePickupDay(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pickup-days-all'] }); resetForm(); },
  });
  const deleteMut = useMutation({
    mutationFn: deletePickupDay,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['pickup-days-all'] }),
  });

  const resetForm = () => { setForm({ label: '', value: '', isActive: true, displayOrder: 0 }); setEditId(null); setError(''); };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.label.trim() || !form.value.trim()) { setError('Both label and value are required.'); return; }
    if (editId) updateMut.mutate({ id: editId, data: form });
    else createMut.mutate(form);
  };

  const startEdit = (d: PickupDayOption) => {
    setEditId(d.id);
    setForm({ label: d.label, value: d.value, isActive: d.isActive, displayOrder: d.displayOrder });
    setError('');
  };

  return (
    <div className="space-y-6">
      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h2 className="text-base font-semibold text-gray-800 mb-1">
          {editId ? '✏️ Edit Pickup Day' : '➕ Add Pickup Day'}
        </h2>
        <p className="text-xs text-gray-500 mb-4">
          <strong>Label</strong> is shown to the guest (e.g. "Monday, 25 Feb 2026"). <strong>Value</strong> is stored internally (e.g. "2026-02-25").
        </p>
        <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
          <div className="flex-1 min-w-[180px]">
            <label className="block text-xs font-medium text-gray-600 mb-1">Display Label *</label>
            <input
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="e.g. Monday, 25 Feb 2026"
              value={form.label}
              onChange={e => setForm(f => ({ ...f, label: e.target.value }))}
            />
          </div>
          <div className="flex-1 min-w-[140px]">
            <label className="block text-xs font-medium text-gray-600 mb-1">Internal Value *</label>
            <input
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="e.g. 2026-02-25"
              value={form.value}
              onChange={e => setForm(f => ({ ...f, value: e.target.value }))}
            />
          </div>
          <div className="w-24">
            <label className="block text-xs font-medium text-gray-600 mb-1">Order</label>
            <input type="number" min={0}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              value={form.displayOrder}
              onChange={e => setForm(f => ({ ...f, displayOrder: parseInt(e.target.value) || 0 }))}
            />
          </div>
          <div className="flex items-center gap-2 pb-1">
            <input type="checkbox" id="day-active" checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
              className="w-4 h-4 accent-green-600" />
            <label htmlFor="day-active" className="text-sm text-gray-600">Active</label>
          </div>
          <button type="submit"
            disabled={createMut.isPending || updateMut.isPending}
            className="bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-green-800 disabled:opacity-50">
            {editId ? 'Update' : 'Add Day'}
          </button>
          {editId && (
            <button type="button" onClick={resetForm}
              className="bg-gray-200 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-300">
              Cancel
            </button>
          )}
        </form>
        {error && <p className="text-red-500 text-xs mt-2">{error}</p>}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <span className="text-sm font-semibold text-gray-700">Pickup Days ({days.length})</span>
        </div>
        {isLoading ? (
          <div className="p-6 text-center text-gray-400 text-sm">Loading...</div>
        ) : days.length === 0 ? (
          <div className="p-6 text-center text-gray-400 text-sm">No pickup days added yet.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase">
              <tr>
                <th className="px-4 py-2 text-left">Label</th>
                <th className="px-4 py-2 text-left">Value</th>
                <th className="px-4 py-2 text-center">Order</th>
                <th className="px-4 py-2 text-center">Status</th>
                <th className="px-4 py-2 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {days.map(d => (
                <tr key={d.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{d.label}</td>
                  <td className="px-4 py-3 text-gray-500 font-mono text-xs">{d.value}</td>
                  <td className="px-4 py-3 text-center text-gray-500">{d.displayOrder}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${d.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                      {d.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right space-x-2">
                    <button onClick={() => startEdit(d)} className="text-blue-600 hover:text-blue-800 text-xs font-medium">Edit</button>
                    <button onClick={() => { if (confirm(`Delete "${d.label}"?`)) deleteMut.mutate(d.id); }}
                      className="text-red-500 hover:text-red-700 text-xs font-medium">Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

// ─── Pickup Hours Panel ───────────────────────────────────────────────────────

function PickupHoursPanel() {
  const qc = useQueryClient();
  const { data: hours = [], isLoading } = useQuery({ queryKey: ['pickup-hours-all'], queryFn: getAllPickupHours });
  const [form, setForm] = useState({ label: '', value: '', isActive: true, displayOrder: 0 });
  const [editId, setEditId] = useState<string | null>(null);
  const [error, setError] = useState('');

  const createMut = useMutation({
    mutationFn: createPickupHour,
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pickup-hours-all'] }); resetForm(); },
  });
  const updateMut = useMutation({
    mutationFn: ({ id, data }: { id: string; data: Omit<PickupHourOption, 'id'> }) => updatePickupHour(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pickup-hours-all'] }); resetForm(); },
  });
  const deleteMut = useMutation({
    mutationFn: deletePickupHour,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['pickup-hours-all'] }),
  });

  const resetForm = () => { setForm({ label: '', value: '', isActive: true, displayOrder: 0 }); setEditId(null); setError(''); };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.label.trim() || !form.value.trim()) { setError('Both label and value are required.'); return; }
    if (editId) updateMut.mutate({ id: editId, data: form });
    else createMut.mutate(form);
  };

  const startEdit = (h: PickupHourOption) => {
    setEditId(h.id);
    setForm({ label: h.label, value: h.value, isActive: h.isActive, displayOrder: h.displayOrder });
    setError('');
  };

  return (
    <div className="space-y-6">
      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h2 className="text-base font-semibold text-gray-800 mb-1">
          {editId ? '✏️ Edit Pickup Hour' : '➕ Add Pickup Hour'}
        </h2>
        <p className="text-xs text-gray-500 mb-4">
          <strong>Label</strong> is shown to the guest (e.g. "09:00 AM"). <strong>Value</strong> is stored internally (e.g. "09:00").
        </p>
        <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
          <div className="flex-1 min-w-[160px]">
            <label className="block text-xs font-medium text-gray-600 mb-1">Display Label *</label>
            <input
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="e.g. 09:00 AM"
              value={form.label}
              onChange={e => setForm(f => ({ ...f, label: e.target.value }))}
            />
          </div>
          <div className="flex-1 min-w-[120px]">
            <label className="block text-xs font-medium text-gray-600 mb-1">Internal Value *</label>
            <input
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              placeholder="e.g. 09:00"
              value={form.value}
              onChange={e => setForm(f => ({ ...f, value: e.target.value }))}
            />
          </div>
          <div className="w-24">
            <label className="block text-xs font-medium text-gray-600 mb-1">Order</label>
            <input type="number" min={0}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500"
              value={form.displayOrder}
              onChange={e => setForm(f => ({ ...f, displayOrder: parseInt(e.target.value) || 0 }))}
            />
          </div>
          <div className="flex items-center gap-2 pb-1">
            <input type="checkbox" id="hour-active" checked={form.isActive}
              onChange={e => setForm(f => ({ ...f, isActive: e.target.checked }))}
              className="w-4 h-4 accent-green-600" />
            <label htmlFor="hour-active" className="text-sm text-gray-600">Active</label>
          </div>
          <button type="submit"
            disabled={createMut.isPending || updateMut.isPending}
            className="bg-green-700 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-green-800 disabled:opacity-50">
            {editId ? 'Update' : 'Add Hour'}
          </button>
          {editId && (
            <button type="button" onClick={resetForm}
              className="bg-gray-200 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-300">
              Cancel
            </button>
          )}
        </form>
        {error && <p className="text-red-500 text-xs mt-2">{error}</p>}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="px-5 py-3 border-b border-gray-100 bg-gray-50">
          <span className="text-sm font-semibold text-gray-700">Pickup Hours ({hours.length})</span>
        </div>
        {isLoading ? (
          <div className="p-6 text-center text-gray-400 text-sm">Loading...</div>
        ) : hours.length === 0 ? (
          <div className="p-6 text-center text-gray-400 text-sm">No pickup hours added yet.</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase">
              <tr>
                <th className="px-4 py-2 text-left">Label</th>
                <th className="px-4 py-2 text-left">Value</th>
                <th className="px-4 py-2 text-center">Order</th>
                <th className="px-4 py-2 text-center">Status</th>
                <th className="px-4 py-2 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {hours.map(h => (
                <tr key={h.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 font-medium text-gray-800">{h.label}</td>
                  <td className="px-4 py-3 text-gray-500 font-mono text-xs">{h.value}</td>
                  <td className="px-4 py-3 text-center text-gray-500">{h.displayOrder}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${h.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                      {h.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right space-x-2">
                    <button onClick={() => startEdit(h)} className="text-blue-600 hover:text-blue-800 text-xs font-medium">Edit</button>
                    <button onClick={() => { if (confirm(`Delete "${h.label}"?`)) deleteMut.mutate(h.id); }}
                      className="text-red-500 hover:text-red-700 text-xs font-medium">Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
