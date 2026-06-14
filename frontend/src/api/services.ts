import apiClient from './client'
import type {
  LoginResponse,
  Guest,
  GuestSummary,
  ArrivalFlightGroup,
  GuestStatus,
  Vehicle,
  VehicleWithStatus,
  Alert,
  AlertSeverity,
  DashboardSummary,
  CreateDepartureRequest,
  GuestStatusHistoryItem,
  InboundStatus,
  OutboundStatus,
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
  getArrivalFlights: async (): Promise<ArrivalFlightGroup[]> => {
    const { data } = await apiClient.get<ArrivalFlightGroup[]>('/guests/arrival-flights')
    return data
  },

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

  // ─── Status Flow ─────────────────────────────────────────────────────────────

  setInboundStatus: async (id: string, status: InboundStatus, notes?: string, hotelName?: string, roomNumber?: string): Promise<void> => {
    await apiClient.post(`/guests/${id}/inbound-status`, { status, notes, hotelName, roomNumber })
  },

  setOutboundStatus: async (id: string, status: OutboundStatus, notes?: string): Promise<void> => {
    await apiClient.post(`/guests/${id}/outbound-status`, { status, notes })
  },

  undoInboundStatus: async (id: string): Promise<void> => {
    await apiClient.post(`/guests/${id}/inbound-status/undo`, {})
  },

  undoOutboundStatus: async (id: string): Promise<void> => {
    await apiClient.post(`/guests/${id}/outbound-status/undo`, {})
  },

  forceStatus: async (
    id: string,
    body: { track: 'Inbound' | 'Outbound'; status: number; notes?: string }
  ): Promise<void> => {
    await apiClient.post(`/guests/${id}/status/force`, body)
  },

  acknowledgeFlightChanges: async (id: string): Promise<void> => {
    await apiClient.post(`/guests/${id}/acknowledge-flight-changes`, {})
  },

  getStatusHistory: async (id: string): Promise<GuestStatusHistoryItem[]> => {
    const { data } = await apiClient.get<{ history: any[] }>(`/guests/${id}/journey-status`)
    // The API returns track as integer (0=Inbound,1=Outbound) and changedByRole as integer;
    // normalize both to human-readable strings for the frontend.
    const roleMap: Record<number, string> = { 0: 'Admin', 1: 'Airport', 2: 'Transport', 3: 'Hotel', 4: 'ControlRoom', 5: 'Liaison', 6: 'AirportView' }
    return (data.history ?? []).map((h) => ({
      ...h,
      track: (h.track === 0 || h.track === 'Inbound') ? 'Inbound' : 'Outbound',
      changedByRole: typeof h.changedByRole === 'number' ? roleMap[h.changedByRole] : h.changedByRole,
    })) as GuestStatusHistoryItem[]
  },
}

// ─── Vehicles ─────────────────────────────────────────────────────────────────

export const vehiclesApi = {
  getAvailable: async (): Promise<Vehicle[]> => {
    const { data } = await apiClient.get<Vehicle[]>('/vehicles/available')
    return data
  },

  getAllWithStatus: async (): Promise<VehicleWithStatus[]> => {
    const { data } = await apiClient.get<VehicleWithStatus[]>('/vehicles/all-with-status')
    return data
  },

  assign: async (guestId: string, vehicleId: string, notes?: string, estimatedArrivalTime?: string): Promise<void> => {
    await apiClient.post('/vehicles/assign', { guestId, vehicleId, notes, estimatedArrivalTime })
  },

  forceAssign: async (guestId: string, vehicleId: string, notes?: string, estimatedArrivalTime?: string, assignmentType?: string): Promise<{ displacedGuestName?: string }> => {
    const { data } = await apiClient.post<{ displacedGuestName?: string }>('/fleet/force-assign', {
      vehicleId,
      guestId,
      assignmentType: assignmentType ?? 'Dedicated',
      notes: notes ?? null,
    })
    return data
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

  getMyNotifications: async (): Promise<Alert[]> => {
    const { data } = await apiClient.get<Alert[]>('/alerts/my')
    return data
  },

  markRead: async (id: string): Promise<void> => {
    await apiClient.patch(`/alerts/${id}/read`, {})
  },
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

export interface RegistrationTypeStats {
  registrationTypeName: string
  total: number
  arrivalScheduled: number
  arrived: number
  inTransitToHotel: number
  atHotel: number
  departed: number
}

// ─── Control Room Dashboard Types ─────────────────────────────────────────────

export interface FleetByClassItem {
  classId: string
  className: string
  classColor?: string
  sortOrder: number
  totalVehicles: number
  available: number
  assigned: number
  outOfService: number
  notProvided: number
  guestsDeserving: number
}

export interface ReceptionSummary {
  totalArriving: number
  scheduled: number
  arrivedAtAirport: number
  receivedByEmbassy: number
  inTransitToHotel: number
  atHotel: number
  everArrived: number
  everReceived: number
  inTransferToAirport: number
  atAirport: number
  boardingCompleted: number
  flights: ReceptionFlight[]
}

export interface ReceptionFlight {
  flightId: string
  flightNumber: string
  airlineName: string
  scheduledArrival: string
  flightStatus: string
  liveDelayMinutes?: number
  totalGuests: number
  scheduled: number
  arrivedAtAirport: number
  receivedByEmbassy: number
  inTransitToHotel: number
}

export interface HotelGuestCount {
  hotelName: string
  guestCount: number
  withRoomCount: number
  noRoomCount: number
}

export interface HotelSummary {
  totalAtHotel: number
  enRouteToHotel: number
  departingActive: number
  noRoomAssigned: number
  byHotel: HotelGuestCount[]
  outboundAtHotel: number
  inTransferToAirport: number
  atAirport: number
  boardingCompleted: number
}

export interface DayArrival {
  dateLabel: string
  dateIso: string
  scheduled: number
  arrived: number
  pending: number
}

export interface ArrivalsByDay {
  days: DayArrival[]
}

export interface ActivityFeedItem {
  type: string
  actorName: string
  actorInitials: string
  action: string
  guestName?: string
  occurredAt: string
}

export interface HourlyActivity {
  hourLabel: string
  count: number
}

export interface UserActivitySummary {
  activeUsers: number
  totalActions: number
  guestUpdates: number
  assignments: number
  hotelUpdates: number
  markedAsArrived: number
  recentFeed: ActivityFeedItem[]
  hourlyBreakdown: HourlyActivity[]
}

export interface DepartureHotelStat {
  hotelId: string
  hotelName: string
  count: number
}

export interface DepartureHourStat {
  hourId: string
  hourLabel: string
  displayOrder: number
  count: number
  byHotel: DepartureHotelStat[]
}

export interface DepartureDayStat {
  dayId: string
  dayLabel: string
  displayOrder: number
  count: number
  byHour: DepartureHourStat[]
}

export interface DepartureStats {
  totalRegistrations: number
  byHotel: DepartureHotelStat[]
  byDay: DepartureDayStat[]
}

export const dashboardApi = {
  getSummary: async (): Promise<DashboardSummary> => {
    const { data } = await apiClient.get<DashboardSummary>('/dashboard/summary')
    return data
  },

  getRegistrationTypeStats: async (): Promise<RegistrationTypeStats[]> => {
    const { data } = await apiClient.get<RegistrationTypeStats[]>('/dashboard/registration-type-stats')
    return data
  },

  getHotelSummary: async (): Promise<HotelSummary> => {
    const { data } = await apiClient.get<HotelSummary>('/dashboard/hotel-summary')
    return data
  },

  getReceptionSummary: async (from?: string, to?: string): Promise<ReceptionSummary> => {
    const params: Record<string, string> = {}
    if (from) params.from = from
    if (to) params.to = to
    const { data } = await apiClient.get<ReceptionSummary>('/dashboard/reception-summary', { params })
    return data
  },

  getArrivalsByDay: async (): Promise<ArrivalsByDay> => {
    const { data } = await apiClient.get<ArrivalsByDay>('/dashboard/arrivals-by-day')
    return data
  },

  getUserActivity: async (): Promise<UserActivitySummary> => {
    const { data } = await apiClient.get<UserActivitySummary>('/dashboard/user-activity')
    return data
  },
}

// ─── Departure Requests ───────────────────────────────────────────────────────

export const departureApi = {
  create: async (request: CreateDepartureRequest): Promise<{ id: string; message: string }> => {
    const { data } = await apiClient.post<{ id: string; message: string }>('/departurerequests', request)
    return data
  },

  getStats: async (): Promise<DepartureStats> => {
    const { data } = await apiClient.get<DepartureStats>('/departure-requests/stats')
    return data
  },
}

// ─── Access Control ─────────────────────────────────────────────────────────────────────────────

export interface PagePermissionDto {
  role: string
  pageId: string
  isGranted: boolean
}

export interface MyPermissionsResponse {
  isAdmin: boolean
  grantedPageIds: string[]
}

export const accessControlApi = {
  getMyPermissions: async (): Promise<MyPermissionsResponse> => {
    const { data } = await apiClient.get<MyPermissionsResponse>('/access-control/my-permissions')
    return data
  },
  getAllPermissions: async (): Promise<PagePermissionDto[]> => {
    const { data } = await apiClient.get<PagePermissionDto[]>('/access-control/permissions')
    return data
  },
  updatePermissions: async (permissions: PagePermissionDto[]): Promise<void> => {
    await apiClient.put('/access-control/permissions', { permissions })
  },
}
