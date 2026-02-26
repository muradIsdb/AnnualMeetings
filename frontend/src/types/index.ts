// ─── Enums ────────────────────────────────────────────────────────────────────

export enum GuestStatus {
  Expected = 0,
  ArrivedAtAirport = 1,
  PassedPassportControl = 2,
  LuggageReceived = 3,
  ReceivedByEmbassy = 4,
  OnTheWayToHotel = 5,
  AtHotel = 6,
  DepartingHotel = 7,
  AtAirportDeparture = 8,
  Departed = 9,
}

export enum AlertSeverity {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3,
}

export enum UserRole {
  AirportReceptionSpecialist = 'AirportReceptionSpecialist',
  TransportationSpecialist = 'TransportationSpecialist',
  AccommodationSpecialist = 'AccommodationSpecialist',
  ControlRoom = 'ControlRoom',
  Administrator = 'Administrator',
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

export interface StaffUser {
  id: string
  email: string
  fullName: string
  role: UserRole
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
  user: StaffUser
}

// ─── Guests ───────────────────────────────────────────────────────────────────

export interface GuestSummary {
  id: string
  fullName: string
  designation?: string
  nationality?: string
  photoUrl?: string
  isCritical: boolean
  requiresAccessibility: boolean
  statusLabel: string
  activeVehiclePlate?: string
  notes?: string
}

export interface FlightSummary {
  id: string
  flightNumber: string
  airline?: string
  scheduledArrival?: string
  actualArrival?: string
  scheduledDeparture?: string
  status?: string
  isArrival: boolean
  delayMinutes?: number
}

export interface ChecklistCompletion {
  checklistItemId: string
  itemName: string
  order: number
  isCompleted: boolean
  completedAt?: string
  completedByName?: string
}

export interface VehicleAssignment {
  id: string
  vehicleMake: string
  vehicleModel: string
  licensePlate: string
  driverName?: string
  driverPhone?: string
  estimatedArrivalTime?: string
  assignedAt: string
}

export interface Guest {
  id: string
  eventsAirContactId: string
  fullName: string
  title?: string
  organization?: string
  designation?: string
  nationality?: string
  photoUrl?: string
  mobileNumber?: string
  email?: string
  isCritical: boolean
  requiresAccessibility: boolean
  status: GuestStatus
  statusLabel: string
  groupCode?: string
  roomNumber?: string
  hotelName?: string
  specialRequirements?: string
  notes?: string
  flights: FlightSummary[]
  checklistCompletions: ChecklistCompletion[]
  activeVehicleAssignment?: VehicleAssignment
}

// ─── Vehicles ─────────────────────────────────────────────────────────────────

export interface Vehicle {
  id: string
  licensePlate: string
  make: string
  model: string
  color?: string
  driverName?: string
  driverPhone?: string
  status: string
  barcodeValue?: string
}

// ─── Alerts ───────────────────────────────────────────────────────────────────

export interface Alert {
  id: string
  guestId?: string
  guestName?: string
  title: string
  message: string
  severity: AlertSeverity
  severityLabel: string
  isResolved: boolean
  createdAt: string
  resolvedAt?: string
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

export interface GuestStatusGroup {
  statusLabel: string
  count: number
  guests: GuestSummary[]
}

export interface DashboardSummary {
  totalGuests: number
  arrivingCount: number
  receivedByEmbassyCount: number
  onTheWayToHotelCount: number
  atHotelCount: number
  departingCount: number
  activeAlertsCount: number
  criticalAlertsCount: number
  activeAlerts: Alert[]
  guestsByStatus: GuestStatusGroup[]
}

// ─── Departure Requests ───────────────────────────────────────────────────────

export interface CreateDepartureRequest {
  guestName: string
  guestEmail?: string
  guestPhone?: string
  hotelName?: string
  roomNumber?: string
  requestedPickupTime: string
  destinationAirport: string
  flightNumber?: string
  specialRequirements?: string
}
