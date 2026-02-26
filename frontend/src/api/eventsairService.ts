import apiClient from './client';

export interface EventsAirConfigDto {
  id: string;
  clientId: string;
  clientSecret: string;
  apiBaseUrl: string;
  tokenEndpoint: string;
  eventCode: string;
  tenantCode: string;
  syncIntervalMinutes: number;
  autoSyncEnabled: boolean;
  syncOnStartup: boolean;
  lastSyncAt: string | null;
  lastSyncStatus: string;
  lastSyncMessage: string | null;
  lastSyncRecordsCount: number;
  isActive: boolean;
}

export interface UpdateEventsAirConfigRequest {
  clientId: string;
  clientSecret?: string;
  apiBaseUrl: string;
  tokenEndpoint: string;
  eventCode: string;
  tenantCode: string;
  syncIntervalMinutes: number;
  autoSyncEnabled: boolean;
  syncOnStartup: boolean;
  isActive: boolean;
}

export interface TestConnectionRequest {
  clientId: string;
  clientSecret: string;
  apiBaseUrl: string;
  tokenEndpoint: string;
  eventCode: string;
  tenantCode: string;
}

export interface TestConnectionResult {
  success: boolean;
  message: string;
  responseTimeMs?: number;
  tokenPreview?: string;
}

export interface EventsAirSyncLogDto {
  id: string;
  syncedAt: string;
  status: string;
  message: string | null;
  recordsSynced: number;
  durationMs: number;
  syncType: string;
}

export interface TriggerSyncResult {
  success: boolean;
  message: string;
  recordsSynced: number;
  durationMs: number;
}

export const eventsAirService = {
  getConfig: () =>
    apiClient.get<EventsAirConfigDto>('/eventsair/config').then(r => r.data),

  updateConfig: (data: UpdateEventsAirConfigRequest) =>
    apiClient.put<EventsAirConfigDto>('/eventsair/config', data).then(r => r.data),

  testConnection: (data: TestConnectionRequest) =>
    apiClient.post<TestConnectionResult>('/eventsair/test-connection', data).then(r => r.data),

  triggerSync: () =>
    apiClient.post<TriggerSyncResult>('/eventsair/sync').then(r => r.data),

  getSyncLogs: (limit = 20) =>
    apiClient.get<EventsAirSyncLogDto[]>(`/eventsair/sync-logs?limit=${limit}`).then(r => r.data),
};
