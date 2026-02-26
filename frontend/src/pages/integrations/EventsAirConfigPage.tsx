import React, { useState, useEffect } from 'react';
import { eventsAirService, EventsAirConfigDto, EventsAirSyncLogDto } from '../../api/eventsairService';

const STATUS_COLORS: Record<string, string> = {
  Success: 'bg-green-100 text-green-800',
  Failed: 'bg-red-100 text-red-800',
  Partial: 'bg-yellow-100 text-yellow-800',
  Never: 'bg-gray-100 text-gray-600',
  Scheduled: 'bg-blue-100 text-blue-800',
  Manual: 'bg-purple-100 text-purple-800',
};

function StatusBadge({ label, colorKey }: { label: string; colorKey?: string }) {
  const cls = STATUS_COLORS[colorKey ?? label] ?? 'bg-gray-100 text-gray-600';
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${cls}`}>
      {label}
    </span>
  );
}

function formatDate(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString();
}

function formatMs(ms: number) {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

export default function EventsAirConfigPage() {
  const [config, setConfig] = useState<EventsAirConfigDto | null>(null);
  const [logs, setLogs] = useState<EventsAirSyncLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string; responseTimeMs?: number } | null>(null);
  const [syncResult, setSyncResult] = useState<{ success: boolean; message: string; recordsSynced: number; durationMs: number } | null>(null);
  const [showSecret, setShowSecret] = useState(false);

  // Form state
  const [form, setForm] = useState({
    clientId: '',
    clientSecret: '',
    apiBaseUrl: 'https://api.eventsair.com',
    tokenEndpoint: 'https://auth.eventsair.com/connect/token',
    eventCode: '',
    tenantCode: '',
    syncIntervalMinutes: 15,
    autoSyncEnabled: false,
    syncOnStartup: false,
    isActive: false,
  });

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    try {
      const [cfg, syncLogs] = await Promise.all([
        eventsAirService.getConfig(),
        eventsAirService.getSyncLogs(20),
      ]);
      setConfig(cfg);
      setLogs(syncLogs);
      setForm({
        clientId: cfg.clientId,
        clientSecret: '',
        apiBaseUrl: cfg.apiBaseUrl,
        tokenEndpoint: cfg.tokenEndpoint,
        eventCode: cfg.eventCode,
        tenantCode: cfg.tenantCode,
        syncIntervalMinutes: cfg.syncIntervalMinutes,
        autoSyncEnabled: cfg.autoSyncEnabled,
        syncOnStartup: cfg.syncOnStartup,
        isActive: cfg.isActive,
      });
    } catch (e) {
      setSaveError('Failed to load configuration.');
    } finally {
      setLoading(false);
    }
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setSaveSuccess(false);
    setSaveError(null);
    try {
      const updated = await eventsAirService.updateConfig({
        ...form,
        clientSecret: form.clientSecret || undefined,
      });
      setConfig(updated);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch {
      setSaveError('Failed to save configuration. Please try again.');
    } finally {
      setSaving(false);
    }
  }

  async function handleTestConnection() {
    setTesting(true);
    setTestResult(null);
    try {
      const result = await eventsAirService.testConnection({
        clientId: form.clientId,
        clientSecret: form.clientSecret,
        apiBaseUrl: form.apiBaseUrl,
        tokenEndpoint: form.tokenEndpoint,
        eventCode: form.eventCode,
        tenantCode: form.tenantCode,
      });
      setTestResult(result);
    } catch {
      setTestResult({ success: false, message: 'Network error. Could not reach the server.' });
    } finally {
      setTesting(false);
    }
  }

  async function handleTriggerSync() {
    setSyncing(true);
    setSyncResult(null);
    try {
      const result = await eventsAirService.triggerSync();
      setSyncResult(result);
      // Reload logs after sync
      const updatedLogs = await eventsAirService.getSyncLogs(20);
      setLogs(updatedLogs);
      const updatedConfig = await eventsAirService.getConfig();
      setConfig(updatedConfig);
    } catch {
      setSyncResult({ success: false, message: 'Sync failed. Please check the configuration.', recordsSynced: 0, durationMs: 0 });
    } finally {
      setSyncing(false);
    }
  }

  const field = (key: keyof typeof form) => ({
    value: form[key] as string | number | boolean,
    onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
      const val = e.target.type === 'checkbox'
        ? (e.target as HTMLInputElement).checked
        : e.target.type === 'number'
          ? Number(e.target.value)
          : e.target.value;
      setForm(prev => ({ ...prev, [key]: val }));
    },
  });

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-green-700"></div>
        <span className="ml-3 text-gray-500">Loading configuration...</span>
      </div>
    );
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6 p-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <span className="text-2xl">🔗</span> EventsAir Integration
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            Configure and manage the connection between the Hospitality Platform and EventsAir.
          </p>
        </div>
        <div className="flex items-center gap-3">
          {config && (
            <div className="flex items-center gap-2 text-sm">
              <span className="text-gray-500">Status:</span>
              <StatusBadge label={config.isActive ? 'Active' : 'Inactive'} colorKey={config.isActive ? 'Success' : 'Never'} />
            </div>
          )}
        </div>
      </div>

      {/* Last Sync Summary Bar */}
      {config && (
        <div className="bg-white border border-gray-200 rounded-xl p-4 flex flex-wrap gap-6 items-center shadow-sm">
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide">Last Sync</p>
            <p className="text-sm font-medium text-gray-800">{formatDate(config.lastSyncAt)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide">Last Status</p>
            <StatusBadge label={config.lastSyncStatus} />
          </div>
          <div>
            <p className="text-xs text-gray-400 uppercase tracking-wide">Records Synced</p>
            <p className="text-sm font-medium text-gray-800">{config.lastSyncRecordsCount.toLocaleString()}</p>
          </div>
          {config.lastSyncMessage && (
            <div className="flex-1">
              <p className="text-xs text-gray-400 uppercase tracking-wide">Message</p>
              <p className="text-sm text-gray-600 truncate max-w-xs">{config.lastSyncMessage}</p>
            </div>
          )}
          <div className="ml-auto">
            <button
              onClick={handleTriggerSync}
              disabled={syncing || !config.isActive}
              className="flex items-center gap-2 bg-green-700 hover:bg-green-800 disabled:bg-gray-300 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
            >
              {syncing ? (
                <>
                  <span className="animate-spin">⟳</span> Syncing...
                </>
              ) : (
                <>
                  ⟳ Sync Now
                </>
              )}
            </button>
          </div>
        </div>
      )}

      {/* Sync Result Banner */}
      {syncResult && (
        <div className={`rounded-xl p-4 flex items-start gap-3 ${syncResult.success ? 'bg-green-50 border border-green-200' : 'bg-red-50 border border-red-200'}`}>
          <span className="text-xl">{syncResult.success ? '✅' : '❌'}</span>
          <div>
            <p className={`font-medium text-sm ${syncResult.success ? 'text-green-800' : 'text-red-800'}`}>
              {syncResult.success ? 'Sync Completed' : 'Sync Failed'}
            </p>
            <p className="text-sm text-gray-600 mt-0.5">{syncResult.message}</p>
            {syncResult.success && (
              <p className="text-xs text-gray-500 mt-1">
                {syncResult.recordsSynced} records synced in {formatMs(syncResult.durationMs)}
              </p>
            )}
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Configuration Form */}
        <div className="lg:col-span-2">
          <form onSubmit={handleSave} className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-gray-100 bg-gray-50">
              <h2 className="text-base font-semibold text-gray-800">API Credentials & Settings</h2>
              <p className="text-xs text-gray-500 mt-0.5">These credentials are used to authenticate with the EventsAir OAuth2 endpoint.</p>
            </div>

            <div className="p-6 space-y-5">
              {/* Integration Active Toggle */}
              <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg border border-gray-200">
                <div>
                  <p className="text-sm font-medium text-gray-800">Integration Active</p>
                  <p className="text-xs text-gray-500">Enable or disable the EventsAir integration entirely.</p>
                </div>
                <label className="relative inline-flex items-center cursor-pointer">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={field('isActive').onChange as any}
                    className="sr-only peer"
                  />
                  <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-green-700"></div>
                </label>
              </div>

              {/* Credentials Section */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Client ID <span className="text-red-500">*</span></label>
                  <input
                    type="text"
                    value={form.clientId}
                    onChange={field('clientId').onChange as any}
                    placeholder="your-client-id"
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">
                    Client Secret {config?.clientSecret === '••••••••••••' && <span className="text-gray-400 font-normal">(leave blank to keep existing)</span>}
                  </label>
                  <div className="relative">
                    <input
                      type={showSecret ? 'text' : 'password'}
                      value={form.clientSecret}
                      onChange={field('clientSecret').onChange as any}
                      placeholder={config?.clientSecret === '••••••••••••' ? '••••••••••••' : 'your-client-secret'}
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent pr-10"
                    />
                    <button
                      type="button"
                      onClick={() => setShowSecret(!showSecret)}
                      className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600 text-xs"
                    >
                      {showSecret ? '🙈' : '👁'}
                    </button>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Event Code <span className="text-red-500">*</span></label>
                  <input
                    type="text"
                    value={form.eventCode}
                    onChange={field('eventCode').onChange as any}
                    placeholder="ISDB2026"
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Tenant Code <span className="text-red-500">*</span></label>
                  <input
                    type="text"
                    value={form.tenantCode}
                    onChange={field('tenantCode').onChange as any}
                    placeholder="isdb"
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                  />
                </div>
              </div>

              {/* Endpoint URLs */}
              <div className="space-y-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">API Base URL</label>
                  <input
                    type="url"
                    value={form.apiBaseUrl}
                    onChange={field('apiBaseUrl').onChange as any}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">OAuth2 Token Endpoint</label>
                  <input
                    type="url"
                    value={form.tokenEndpoint}
                    onChange={field('tokenEndpoint').onChange as any}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                  />
                </div>
              </div>

              {/* Sync Settings */}
              <div className="border-t border-gray-100 pt-4">
                <h3 className="text-sm font-semibold text-gray-700 mb-3">Synchronisation Settings</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-600 mb-1">Sync Interval (minutes)</label>
                    <input
                      type="number"
                      min={5}
                      max={1440}
                      value={form.syncIntervalMinutes}
                      onChange={field('syncIntervalMinutes').onChange as any}
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-600 focus:border-transparent"
                    />
                    <p className="text-xs text-gray-400 mt-1">Minimum: 5 minutes. Recommended: 15 minutes.</p>
                  </div>
                  <div className="space-y-3 pt-1">
                    <label className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={form.autoSyncEnabled}
                        onChange={field('autoSyncEnabled').onChange as any}
                        className="w-4 h-4 text-green-700 rounded border-gray-300 focus:ring-green-600"
                      />
                      <div>
                        <p className="text-sm font-medium text-gray-700">Automatic Sync</p>
                        <p className="text-xs text-gray-400">Run sync on the configured interval.</p>
                      </div>
                    </label>
                    <label className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={form.syncOnStartup}
                        onChange={field('syncOnStartup').onChange as any}
                        className="w-4 h-4 text-green-700 rounded border-gray-300 focus:ring-green-600"
                      />
                      <div>
                        <p className="text-sm font-medium text-gray-700">Sync on Startup</p>
                        <p className="text-xs text-gray-400">Run a full sync when the server starts.</p>
                      </div>
                    </label>
                  </div>
                </div>
              </div>
            </div>

            {/* Form Actions */}
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-between gap-3">
              <div>
                {saveSuccess && (
                  <p className="text-sm text-green-700 font-medium flex items-center gap-1">
                    ✅ Configuration saved successfully.
                  </p>
                )}
                {saveError && (
                  <p className="text-sm text-red-600 font-medium flex items-center gap-1">
                    ❌ {saveError}
                  </p>
                )}
              </div>
              <div className="flex gap-3">
                <button
                  type="button"
                  onClick={handleTestConnection}
                  disabled={testing}
                  title={!form.clientId ? 'Enter a Client ID to test the connection' : 'Test the EventsAir API connection'}
                  className="flex items-center gap-2 border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-50 text-gray-700 text-sm font-medium px-4 py-2 rounded-lg transition-colors"
                >
                  {testing ? (
                    <><span className="animate-spin">⟳</span> Testing...</>
                  ) : (
                    <>🔌 Test Connection</>
                  )}
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="flex items-center gap-2 bg-green-700 hover:bg-green-800 disabled:bg-green-400 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
                >
                  {saving ? (
                    <><span className="animate-spin">⟳</span> Saving...</>
                  ) : (
                    <>💾 Save Configuration</>
                  )}
                </button>
              </div>
            </div>
          </form>

          {/* Test Connection Result */}
          {testResult && (
            <div className={`mt-4 rounded-xl p-4 border ${
              testResult.success
                ? 'bg-green-50 border-green-200'
                : 'bg-amber-50 border-amber-200'
            }`}>
              <div className="flex items-start gap-3">
                <span className="text-xl mt-0.5">{testResult.success ? '✅' : '⚠️'}</span>
                <div className="flex-1">
                  <p className={`font-semibold text-sm ${
                    testResult.success ? 'text-green-800' : 'text-amber-800'
                  }`}>
                    {testResult.success ? 'Connection Successful' : 'Connection Test Result'}
                  </p>
                  <p className="text-sm text-gray-700 mt-1">{testResult.message}</p>
                  {testResult.responseTimeMs !== undefined && testResult.responseTimeMs > 0 && (
                    <p className="text-xs text-gray-400 mt-1">Response time: {formatMs(testResult.responseTimeMs)}</p>
                  )}
                  {!testResult.success && (
                    <p className="text-xs text-gray-500 mt-2 bg-white/60 rounded p-2">
                      <strong>Tip:</strong> Enter your EventsAir Client ID and Client Secret, click <em>Save Configuration</em> first, then click <em>Test Connection</em> again.
                    </p>
                  )}
                </div>
                <button
                  onClick={() => setTestResult(null)}
                  className="text-gray-400 hover:text-gray-600 text-lg leading-none"
                  title="Dismiss"
                >×</button>
              </div>
            </div>
          )}
        </div>

        {/* Right Sidebar: Info + Quick Stats */}
        <div className="space-y-4">
          {/* Integration Info Card */}
          <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
            <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
              <h3 className="text-sm font-semibold text-gray-700">Integration Overview</h3>
            </div>
            <div className="p-4 space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-500">Protocol</span>
                <span className="font-medium text-gray-800">OAuth 2.0 + GraphQL</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Auth Flow</span>
                <span className="font-medium text-gray-800">Client Credentials</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Data Direction</span>
                <span className="font-medium text-gray-800">EventsAir → Platform</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Sync Mode</span>
                <span className="font-medium text-gray-800">{form.autoSyncEnabled ? `Every ${form.syncIntervalMinutes}m` : 'Manual Only'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-500">Total Syncs</span>
                <span className="font-medium text-gray-800">{logs.length}</span>
              </div>
            </div>
          </div>

          {/* Help Card */}
          <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 text-sm space-y-2">
            <p className="font-semibold text-blue-800">📖 Setup Guide</p>
            <ol className="text-blue-700 space-y-1 list-decimal list-inside text-xs">
              <li>Obtain Client ID and Secret from your EventsAir administrator.</li>
              <li>Enter your Event Code and Tenant Code as provided.</li>
              <li>Click <strong>Test Connection</strong> to verify credentials.</li>
              <li>Enable <strong>Integration Active</strong> and save.</li>
              <li>Click <strong>Sync Now</strong> to perform the first data pull.</li>
            </ol>
          </div>
        </div>
      </div>

      {/* Sync History Log */}
      <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <div>
            <h2 className="text-base font-semibold text-gray-800">Synchronisation History</h2>
            <p className="text-xs text-gray-500 mt-0.5">The last 20 sync operations are shown below.</p>
          </div>
          <button
            onClick={loadData}
            className="text-xs text-gray-500 hover:text-gray-700 border border-gray-200 rounded-lg px-3 py-1.5 hover:bg-gray-50 transition-colors"
          >
            ↻ Refresh
          </button>
        </div>

        {logs.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-gray-400">
            <span className="text-4xl mb-3">📋</span>
            <p className="text-sm font-medium">No sync history yet.</p>
            <p className="text-xs mt-1">Run your first sync to see logs here.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50">
                  <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Date & Time</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Type</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Status</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Records</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Duration</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wide">Message</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {logs.map(log => (
                  <tr key={log.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-3 text-gray-700 whitespace-nowrap">{formatDate(log.syncedAt)}</td>
                    <td className="px-4 py-3">
                      <StatusBadge label={log.syncType} />
                    </td>
                    <td className="px-4 py-3">
                      <StatusBadge label={log.status} />
                    </td>
                    <td className="px-4 py-3 text-right font-medium text-gray-800">{log.recordsSynced.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right text-gray-500">{formatMs(log.durationMs)}</td>
                    <td className="px-4 py-3 text-gray-500 max-w-xs truncate">{log.message ?? '—'}</td>
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
