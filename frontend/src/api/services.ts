import apiClient from './client'
import type {
  LoginResponse,
  Guest,
  GuestSummary,
  GuestStatus,
  Vehicle,
  Alert,
  AlertSeverity,
  DashboardSummary,
  CreateDepartureRequest,
} from '../types'

// ─── Auth ─────────────────────────────────────────────────────────────────────

export const authApi = {
  login: async (email: string, password: string): Promise<LoginResponse> => {
    const { data } = await apiClient.post<LoginResponse>('/auth/login', { email, password })
    return data
  },
}

// ─── Guests ───────────────────────────────────────────────────────────────────

export const guestsApi = {
  getAll: async (status?: GuestStatus, isCritical?: boolean): Promise<GuestSummary[]> => {
    const params: Record<string, unknown> = {}
    if (status !== undefined) params.status = status
    if (isCritical !== undefined) params.isCritical = isCritical
    const { data } = await apiClient.get<GuestSummary[]>('/guests', { params })
    return data
  },

  getById: async (id: string): Promise<Guest> => {
    const { data } = await apiClient.get<Guest>(`/guests/${id}`)
    return data
  },

  updateStatus: async (id: string, status: GuestStatus, notes?: string): Promise<void> => {
    await apiClient.patch(`/guests/${id}/status`, { status, notes })
  },

  completeChecklistItem: async (guestId: string, checklistItemId: string, notes?: string): Promise<void> => {
    await apiClient.post(`/guests/${guestId}/checklist/${checklistItemId}`, { notes })
  },
}

// ─── Vehicles ─────────────────────────────────────────────────────────────────

export const vehiclesApi = {
  getAvailable: async (): Promise<Vehicle[]> => {
    const { data } = await apiClient.get<Vehicle[]>('/vehicles/available')
    return data
  },

  assign: async (guestId: string, vehicleId: string, notes?: string, estimatedArrivalTime?: string): Promise<void> => {
    await apiClient.post('/vehicles/assign', { guestId, vehicleId, notes, estimatedArrivalTime })
  },

  assignByBarcode: async (guestId: string, barcodeValue: string, notes?: string): Promise<void> => {
    await apiClient.post('/vehicles/assign-by-barcode', { guestId, barcodeValue, notes })
  },
}

// ─── Alerts ───────────────────────────────────────────────────────────────────

export const alertsApi = {
  create: async (title: string, message: string, severity: AlertSeverity, guestId?: string): Promise<{ id: string }> => {
    const { data } = await apiClient.post<{ id: string }>('/alerts', { guestId, title, message, severity })
    return data
  },

  resolve: async (id: string, resolutionNotes?: string): Promise<void> => {
    await apiClient.post(`/alerts/${id}/resolve`, { resolutionNotes })
  },
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

export const dashboardApi = {
  getSummary: async (): Promise<DashboardSummary> => {
    const { data } = await apiClient.get<DashboardSummary>('/dashboard/summary')
    return data
  },
}

// ─── Departure Requests ───────────────────────────────────────────────────────

export const departureApi = {
  create: async (request: CreateDepartureRequest): Promise<{ id: string; message: string }> => {
    const { data } = await apiClient.post<{ id: string; message: string }>('/departurerequests', request)
    return data
  },
}
