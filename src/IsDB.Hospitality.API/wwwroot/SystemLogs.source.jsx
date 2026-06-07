import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axios from 'axios'; // Assumed imported as _ in the minified bundle
import { FiAlertTriangle, FiTrash2, FiDownload, FiSearch, FiX, FiChevronLeft, FiChevronRight } from 'react-icons/fi'; // Assumed icons available

const severityOptions = [
  { value: "", label: "All Severities" },
  { value: "0", label: "Info" },
  { value: "1", label: "Warning" },
  { value: "2", label: "Error" },
  { value: "3", label: "Critical" }
];

const severityColors = {
  0: { badge: "bg-blue-100 text-blue-700", label: "Info" },
  1: { badge: "bg-yellow-100 text-yellow-800", label: "Warning" },
  2: { badge: "bg-red-100 text-red-700", label: "Error" },
  3: { badge: "bg-red-600 text-white", label: "Critical" }
};

const moduleOptions = [
  { value: "", label: "All Modules" },
  { value: "EventsAirSync", label: "EventsAir Sync" },
  { value: "AviationStackSync", label: "AviationStack Sync" },
  { value: "Auth", label: "Authentication" },
  { value: "Admin", label: "Admin Actions" },
  { value: "System", label: "System" }
];

const pageSizeOptions = [25, 50, 100];

function SeverityBadge({ severity }) {
  const config = severityColors[severity] || severityColors[0];
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${config.badge}`}>
      {config.label}
    </span>
  );
}

function formatDate(dateString) {
  return new Date(dateString).toLocaleString("en-GB", {
    day: "2-digit", month: "short", year: "numeric",
    hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
  });
}

// Modal for clearing logs
function ClearLogsModal({ onClose, onConfirm, isLoading }) {
  const [clearOption, setClearOption] = useState("30");
  
  const options = [
    { value: "7", label: "Older than 7 days" },
    { value: "30", label: "Older than 30 days" },
    { value: "90", label: "Older than 90 days" },
    { value: "all", label: "All logs" }
  ];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6">
        <div className="flex items-center gap-3 mb-4">
          <div className="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center flex-shrink-0">
            <FiAlertTriangle className="w-5 h-5 text-red-600" />
          </div>
          <div>
            <h3 className="text-base font-semibold text-gray-900">Clear System Logs</h3>
            <p className="text-sm text-gray-500">This action cannot be undone.</p>
          </div>
          <button onClick={onClose} className="ml-auto text-gray-400 hover:text-gray-600">
            <FiX className="w-5 h-5" />
          </button>
        </div>
        
        <div className="space-y-2 mb-5">
          {options.map(opt => (
            <label key={opt.value} className="flex items-center gap-3 p-3 rounded-lg border cursor-pointer hover:bg-gray-50 transition-colors">
              <input 
                type="radio" 
                name="clearOption" 
                value={opt.value}
                checked={clearOption === opt.value}
                onChange={() => setClearOption(opt.value)}
                className="accent-red-600"
              />
              <span className="text-sm text-gray-800">{opt.label}</span>
            </label>
          ))}
        </div>
        
        <div className="flex gap-3 justify-end">
          <button onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50">
            Cancel
          </button>
          <button 
            onClick={() => onConfirm(clearOption === "all" ? null : parseInt(clearOption))}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-white bg-red-600 rounded-lg hover:bg-red-700 disabled:opacity-50"
          >
            {isLoading ? "Deleting…" : "Delete Logs"}
          </button>
        </div>
      </div>
    </div>
  );
}

export default function SystemLogs() {
  const queryClient = useQueryClient();
  
  // Filter state
  const [filters, setFilters] = useState({ search: "", severity: "", module: "", from: "", to: "" });
  const [appliedFilters, setAppliedFilters] = useState({ search: "", severity: "", module: "", from: "", to: "" });
  
  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  
  // UI state
  const [showClearModal, setShowClearModal] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [expandedLogId, setExpandedLogId] = useState(null);

  // Fetch data
  const { data, isLoading, isError } = useQuery({
    queryKey: ["system-logs", appliedFilters, page, pageSize],
    queryFn: async () => {
      const params = { page: String(page), pageSize: String(pageSize) };
      if (appliedFilters.search) params.search = appliedFilters.search;
      if (appliedFilters.severity !== "") params.severity = appliedFilters.severity;
      if (appliedFilters.module) params.module = appliedFilters.module;
      if (appliedFilters.from) params.from = appliedFilters.from;
      if (appliedFilters.to) params.to = appliedFilters.to;
      
      // Assumes axios is available globally or imported
      const response = await window._axios.get("/system-logs", { params });
      return response.data;
    },
    keepPreviousData: true
  });

  // Clear logs mutation
  const clearMutation = useMutation({
    mutationFn: async (olderThanDays) => {
      if (olderThanDays === null) {
        return (await window._axios.delete("/system-logs")).data;
      }
      return (await window._axios.delete("/system-logs", { params: { olderThanDays } })).data;
    },
    onSuccess: () => {
      setShowClearModal(false);
      queryClient.invalidateQueries({ queryKey: ["system-logs"] });
    }
  });

  const handleApplyFilters = () => {
    setAppliedFilters({ ...filters });
    setPage(1);
  };

  const handleClearFilters = () => {
    const empty = { search: "", severity: "", module: "", from: "", to: "" };
    setFilters(empty);
    setAppliedFilters(empty);
    setPage(1);
  };

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const params = {};
      if (appliedFilters.search) params.search = appliedFilters.search;
      if (appliedFilters.severity !== "") params.severity = appliedFilters.severity;
      if (appliedFilters.module) params.module = appliedFilters.module;
      if (appliedFilters.from) params.from = appliedFilters.from;
      if (appliedFilters.to) params.to = appliedFilters.to;
      
      const response = await window._axios.get("/system-logs/export", { 
        params, 
        responseType: "blob" 
      });
      
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement("a");
      link.href = url;
      link.download = `system_logs_${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } finally {
      setIsExporting(false);
    }
  };

  const toggleExpand = (id) => {
    setExpandedLogId(prev => prev === id ? null : id);
  };

  const hasActiveFilters = Object.values(appliedFilters).some(v => v !== "");

  return (
    <div className="p-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gray-800 flex items-center justify-center">
            <span className="text-white font-mono text-lg">{">_"}</span>
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">System Logs</h1>
            <p className="text-sm text-gray-500">
              {data ? `${data.total.toLocaleString()} total logs recorded` : "Loading…"}
            </p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <button 
            onClick={() => setShowClearModal(true)}
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-red-700 bg-red-50 border border-red-200 rounded-lg hover:bg-red-100 transition-colors"
          >
            <FiTrash2 className="w-4 h-4" />
            Clear Logs
          </button>
          <button 
            onClick={handleExport}
            disabled={isExporting}
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white bg-gray-800 rounded-lg hover:bg-gray-900 disabled:opacity-50 transition-colors"
          >
            <FiDownload className="w-4 h-4" />
            {isExporting ? "Exporting…" : "Export CSV"}
          </button>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="bg-white rounded-xl border border-gray-200 p-4 mb-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
          <div className="relative lg:col-span-2">
            <FiSearch className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input 
              type="text" 
              placeholder="Search messages or details…" 
              value={filters.search}
              onChange={e => setFilters(f => ({ ...f, search: e.target.value }))}
              onKeyDown={e => e.key === "Enter" && handleApplyFilters()}
              className="w-full pl-9 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-800"
            />
          </div>
          
          <select 
            value={filters.severity}
            onChange={e => setFilters(f => ({ ...f, severity: e.target.value }))}
            className="px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-800"
          >
            {severityOptions.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
          
          <select 
            value={filters.module}
            onChange={e => setFilters(f => ({ ...f, module: e.target.value }))}
            className="px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-800"
          >
            {moduleOptions.map(opt => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
          
          <button 
            onClick={handleApplyFilters}
            className="flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium text-white bg-gray-800 rounded-lg hover:bg-gray-900 transition-colors"
          >
            Apply
          </button>
        </div>
        
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-3">
          <div className="flex items-center gap-2">
            <label className="text-xs text-gray-500 whitespace-nowrap">From</label>
            <input 
              type="date" 
              value={filters.from}
              onChange={e => setFilters(f => ({ ...f, from: e.target.value }))}
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-800"
            />
          </div>
          <div className="flex items-center gap-2">
            <label className="text-xs text-gray-500 whitespace-nowrap">To</label>
            <input 
              type="date" 
              value={filters.to}
              onChange={e => setFilters(f => ({ ...f, to: e.target.value }))}
              className="flex-1 px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-800"
            />
          </div>
          {hasActiveFilters && (
            <button 
              onClick={handleClearFilters}
              className="flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 transition-colors"
            >
              <FiX className="w-4 h-4" />
              Clear filters
            </button>
          )}
        </div>
      </div>

      {/* Data Table */}
      <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
        {isLoading ? (
          <div className="flex items-center justify-center h-48 text-gray-400 text-sm">
            Loading system logs…
          </div>
        ) : isError ? (
          <div className="flex items-center justify-center h-48 text-red-500 text-sm">
            Failed to load system logs. Please try again.
          </div>
        ) : !data || data.items.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-gray-400">
            <span className="text-4xl mb-2 opacity-30">📭</span>
            <p className="text-sm">No system logs found</p>
            {hasActiveFilters && (
              <button onClick={handleClearFilters} className="mt-2 text-sm text-gray-600 hover:underline">
                Clear filters
              </button>
            )}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm min-w-[800px]">
              <thead>
                <tr className="border-b border-gray-200 bg-gray-50">
                  <th className="px-4 py-3 text-left font-semibold text-gray-600 whitespace-nowrap">Date & Time</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-600 whitespace-nowrap">Severity</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-600 whitespace-nowrap">Module</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-600 w-full">Message</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-600 whitespace-nowrap">Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {data.items.map(log => (
                  <React.Fragment key={log.id}>
                    <tr className={`hover:bg-gray-50 transition-colors ${expandedLogId === log.id ? "bg-gray-50" : ""}`}>
                      <td className="px-4 py-3 text-gray-500 whitespace-nowrap text-xs">
                        {formatDate(log.createdAt)}
                      </td>
                      <td className="px-4 py-3">
                        <SeverityBadge severity={log.severity} />
                      </td>
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700">
                          {log.module}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-800 font-medium">
                        {log.message}
                      </td>
                      <td className="px-4 py-3 text-right">
                        {log.detail && (
                          <button 
                            onClick={() => toggleExpand(log.id)}
                            className="text-xs font-medium text-gray-600 hover:text-gray-900 underline decoration-gray-300 underline-offset-2"
                          >
                            {expandedLogId === log.id ? "Hide" : "View"}
                          </button>
                        )}
                      </td>
                    </tr>
                    {expandedLogId === log.id && log.detail && (
                      <tr className="bg-gray-50 border-b border-gray-100">
                        <td colSpan={5} className="px-4 py-3">
                          <div className="bg-gray-900 rounded-lg p-3 overflow-x-auto">
                            <pre className="text-xs text-green-400 font-mono whitespace-pre-wrap break-all">
                              {log.detail}
                            </pre>
                          </div>
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                ))}
              </tbody>
            </table>
          </div>
        )}
        
        {/* Pagination Footer */}
        {data && data.totalPages > 0 && (
          <div className="flex items-center justify-between p-4 border-t border-gray-200 bg-white">
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <span>Show</span>
              <select 
                value={pageSize} 
                onChange={e => { setPageSize(Number(e.target.value)); setPage(1); }}
                className="px-2 py-1 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-gray-800"
              >
                {pageSizeOptions.map(opt => (
                  <option key={opt} value={opt}>{opt}</option>
                ))}
              </select>
              <span>per page</span>
              <span className="ml-2">
                {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, data.total)} of {data.total.toLocaleString()}
              </span>
            </div>
            
            <div className="flex items-center gap-1">
              <button 
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg disabled:opacity-40 disabled:cursor-not-allowed flex items-center"
              >
                <FiChevronLeft className="w-4 h-4" />
                <span className="sr-only">Previous</span>
              </button>
              <span className="px-3 py-1 text-sm text-gray-700">
                Page {page} of {data.totalPages}
              </span>
              <button 
                onClick={() => setPage(p => Math.min(data.totalPages, p + 1))}
                disabled={page >= data.totalPages}
                className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg disabled:opacity-40 disabled:cursor-not-allowed flex items-center"
              >
                <FiChevronRight className="w-4 h-4" />
                <span className="sr-only">Next</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {showClearModal && (
        <ClearLogsModal 
          onClose={() => setShowClearModal(false)}
          onConfirm={(days) => clearMutation.mutate(days)}
          isLoading={clearMutation.isPending}
        />
      )}
    </div>
  );
}
