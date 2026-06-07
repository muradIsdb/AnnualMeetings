/**
 * SyncAlertsPage.source.jsx
 *
 * Source file for the Sync Alerts page (/transport/sync-notifications).
 * Accessible to Admin and Transport roles only.
 *
 * This file is kept in the repository for reference.
 * The compiled output is injected into the main bundle (index-carclass-hist-v29.js)
 * using the patch script: /home/ubuntu/inject_sync_alerts.py
 *
 * API endpoints used:
 *   GET  /api/sync-notifications          - list with filters: type, resolved, search, page, pageSize
 *   POST /api/sync-notifications/{id}/resolve  - mark as resolved (body: { notes })
 *   POST /api/sync-notifications/resolve-all   - bulk resolve all open alerts
 *   GET  /api/sync-notifications/summary       - { guestRemoved, regTypeChanged, carClassMismatch, totalOpen, totalResolved }
 */

import React, { useState, useCallback } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

// ── Constants ─────────────────────────────────────────────────────────────────
const ALERT_TYPE_LABELS = {
  GuestRemoved:      "Guest Removed",
  RegTypeChanged:    "Reg Type Changed",
  CarClassMismatch:  "Car Class Mismatch",
};

const ALERT_TYPE_COLORS = {
  GuestRemoved:     "bg-red-100 text-red-700",
  RegTypeChanged:   "bg-amber-100 text-amber-700",
  CarClassMismatch: "bg-blue-100 text-blue-700",
};

const SOURCE_LABELS = {
  AutoSync:   "Auto Sync",
  ManualSync: "Manual Sync",
};

// ── Summary Cards ─────────────────────────────────────────────────────────────
function SummaryCards({ summary, activeTab, setActiveTab }) {
  const cards = [
    {
      key: "all",
      label: "Total Open",
      value: summary?.totalOpen ?? 0,
      color: "border-l-gray-400",
      textColor: "text-gray-700",
    },
    {
      key: "GuestRemoved",
      label: "Guest Removed",
      value: summary?.guestRemoved ?? 0,
      color: "border-l-red-500",
      textColor: "text-red-600",
    },
    {
      key: "RegTypeChanged",
      label: "Reg Type Changed",
      value: summary?.regTypeChanged ?? 0,
      color: "border-l-amber-500",
      textColor: "text-amber-600",
    },
    {
      key: "CarClassMismatch",
      label: "Car Class Mismatch",
      value: summary?.carClassMismatch ?? 0,
      color: "border-l-blue-500",
      textColor: "text-blue-600",
    },
    {
      key: "resolved",
      label: "Resolved",
      value: summary?.totalResolved ?? 0,
      color: "border-l-green-500",
      textColor: "text-green-600",
    },
  ];

  return (
    <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mb-6">
      {cards.map((c) => (
        <button
          key={c.key}
          onClick={() => setActiveTab(c.key)}
          className={`bg-white rounded-xl border border-gray-100 p-4 shadow-sm border-l-4 ${c.color} text-left transition hover:shadow-md ${activeTab === c.key ? "ring-2 ring-offset-1 ring-blue-400" : ""}`}
        >
          <div className={`text-2xl font-bold ${c.textColor}`}>{c.value}</div>
          <div className="text-xs text-gray-500 mt-1">{c.label}</div>
        </button>
      ))}
    </div>
  );
}

// ── Alert Row ─────────────────────────────────────────────────────────────────
function AlertRow({ alert, onResolve, resolving }) {
  const [notes, setNotes] = useState("");
  const [showNotes, setShowNotes] = useState(false);

  const isResolved = alert.isResolved;
  const typeLabel = ALERT_TYPE_LABELS[alert.alertType] ?? alert.alertType;
  const typeColor = ALERT_TYPE_COLORS[alert.alertType] ?? "bg-gray-100 text-gray-700";
  const sourceLabel = SOURCE_LABELS[alert.syncSource] ?? alert.syncSource;

  return (
    <tr className={isResolved ? "opacity-45 bg-gray-50" : "bg-white hover:bg-gray-50"}>
      <td className="px-4 py-3 text-sm">
        <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-medium ${typeColor}`}>
          {typeLabel}
        </span>
      </td>
      <td className="px-4 py-3">
        <div className="text-sm font-medium text-gray-900">{alert.guestName}</div>
        {alert.eventsAirContactId && (
          <div className="text-xs text-gray-400">{alert.eventsAirContactId}</div>
        )}
      </td>
      <td className="px-4 py-3 text-sm text-gray-600">
        {alert.alertType === "GuestRemoved" && alert.vehiclePlate && (
          <span className="inline-flex items-center gap-1">
            <span className="text-xs bg-gray-100 px-2 py-0.5 rounded font-mono">{alert.vehiclePlate}</span>
          </span>
        )}
        {(alert.alertType === "RegTypeChanged" || alert.alertType === "CarClassMismatch") && (
          <span className="text-xs">
            <span className="text-gray-400">{alert.oldValue}</span>
            <span className="mx-1 text-gray-400">→</span>
            <span className="font-medium text-gray-700">{alert.newValue}</span>
          </span>
        )}
      </td>
      <td className="px-4 py-3 text-xs text-gray-500">
        <div>{sourceLabel}</div>
        <div>{new Date(alert.detectedAt).toLocaleString()}</div>
      </td>
      <td className="px-4 py-3 text-xs text-gray-500">
        {isResolved ? (
          <div>
            <div className="text-green-600 font-medium">Resolved</div>
            <div>{alert.resolvedByName}</div>
            <div>{alert.resolvedAt ? new Date(alert.resolvedAt).toLocaleString() : ""}</div>
            {alert.notes && <div className="italic text-gray-400 mt-1">"{alert.notes}"</div>}
          </div>
        ) : (
          <div>
            {showNotes ? (
              <div className="flex flex-col gap-1">
                <textarea
                  className="border border-gray-200 rounded text-xs p-1 w-40 resize-none"
                  rows={2}
                  placeholder="Optional notes..."
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                />
                <div className="flex gap-1">
                  <button
                    onClick={() => { onResolve(alert.id, notes); setShowNotes(false); }}
                    disabled={resolving}
                    className="text-xs bg-green-600 text-white px-2 py-0.5 rounded hover:bg-green-700 disabled:opacity-50"
                  >
                    Confirm
                  </button>
                  <button
                    onClick={() => setShowNotes(false)}
                    className="text-xs bg-gray-200 text-gray-600 px-2 py-0.5 rounded hover:bg-gray-300"
                  >
                    Cancel
                  </button>
                </div>
              </div>
            ) : (
              <button
                onClick={() => setShowNotes(true)}
                className="text-xs bg-green-50 text-green-700 border border-green-200 px-3 py-1 rounded hover:bg-green-100"
              >
                Mark Resolved
              </button>
            )}
          </div>
        )}
      </td>
    </tr>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────
export default function SyncAlertsPage() {
  const qc = useQueryClient();
  const ax = window._axios;

  const [activeTab, setActiveTab] = useState("all");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Build query params
  const params = useCallback(() => {
    const p = { page, pageSize };
    if (activeTab === "resolved") { p.resolved = true; }
    else if (activeTab !== "all") { p.type = activeTab; p.resolved = false; }
    else { p.resolved = false; }
    if (search.trim()) p.search = search.trim();
    return p;
  }, [activeTab, search, page]);

  const { data, isLoading } = useQuery({
    queryKey: ["sync-notifications", activeTab, search, page],
    queryFn: () => ax.get("/sync-notifications", { params: params() }).then((r) => r.data),
    refetchInterval: 30000,
  });

  const { data: summary } = useQuery({
    queryKey: ["sync-notifications-summary"],
    queryFn: () => ax.get("/sync-notifications/summary").then((r) => r.data),
    refetchInterval: 30000,
  });

  const resolveMut = useMutation({
    mutationFn: ({ id, notes }) => ax.post(`/sync-notifications/${id}/resolve`, { notes }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["sync-notifications"] });
      qc.invalidateQueries({ queryKey: ["sync-notifications-summary"] });
    },
  });

  const resolveAllMut = useMutation({
    mutationFn: () => ax.post("/sync-notifications/resolve-all"),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["sync-notifications"] });
      qc.invalidateQueries({ queryKey: ["sync-notifications-summary"] });
    },
  });

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="p-6 space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-gray-900">Sync Alerts</h1>
          <p className="text-sm text-gray-500 mt-0.5">
            Changes detected during EventsAir synchronisation that require attention.
          </p>
        </div>
        {(summary?.totalOpen ?? 0) > 0 && (
          <button
            onClick={() => resolveAllMut.mutate()}
            disabled={resolveAllMut.isPending}
            className="text-sm bg-green-600 text-white px-4 py-2 rounded-lg hover:bg-green-700 disabled:opacity-50"
          >
            Mark All Resolved
          </button>
        )}
      </div>

      {/* Summary Cards */}
      <SummaryCards summary={summary} activeTab={activeTab} setActiveTab={(t) => { setActiveTab(t); setPage(1); }} />

      {/* Search */}
      <div className="flex gap-3">
        <input
          type="text"
          placeholder="Search by guest name, vehicle plate, or EventsAir ID..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          className="flex-1 border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-300"
        />
      </div>

      {/* Table */}
      <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
        {isLoading ? (
          <div className="p-8 text-center text-gray-400 text-sm">Loading alerts...</div>
        ) : items.length === 0 ? (
          <div className="p-8 text-center text-gray-400 text-sm">
            {activeTab === "resolved" ? "No resolved alerts." : "No open alerts — everything looks good!"}
          </div>
        ) : (
          <table className="w-full text-left">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr>
                <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Type</th>
                <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Guest</th>
                <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Details</th>
                <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Source / Detected</th>
                <th className="px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {items.map((alert) => (
                <AlertRow
                  key={alert.id}
                  alert={alert}
                  onResolve={(id, notes) => resolveMut.mutate({ id, notes })}
                  resolving={resolveMut.isPending}
                />
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-gray-500">
          <span>
            Showing {Math.min((page - 1) * pageSize + 1, totalCount)}–{Math.min(page * pageSize, totalCount)} of {totalCount}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1 border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-40"
            >
              Previous
            </button>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1 border border-gray-200 rounded hover:bg-gray-50 disabled:opacity-40"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
