export enum UserRole {
  Airport = 'Airport',
  Transport = 'Transport',
  Hotel = 'Hotel',
  ControlRoom = 'ControlRoom',
  Admin = 'Admin',
  Liaison = 'Liaison',
}

export interface StaffUser {
  id: string;
  fullName: string;
  name?: string;
  email: string;
  role: UserRole;
  roles?: UserRole[];
  isActive: boolean;
}

export interface LoginResponse {
  token?: string;
  accessToken?: string;
  refreshToken?: string;
  user: StaffUser;
}

export enum GuestStatus {
  Expected = 'Expected',
  AtAirport = 'AtAirport',
  InTransit = 'InTransit',
  AtHotel = 'AtHotel',
  CheckedOut = 'CheckedOut',
  Departed = 'Departed',
}

export enum InboundStatus {
  ArrivalScheduled = 0,
  Arrived = 1,
  ReceivedByEmbassyTeam = 2,
  VehicleAssigned = 3,
  AtHotel = 4,
}

export enum OutboundStatus {
  AtHotel = 0,
  InTransitToAirport = 1,
  AtAirport = 2,
  BoardingCompleted = 3,
}

export enum AlertSeverity {
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  Critical = 'Critical',
}

export interface GuestSummary {
  id: string;
  fullName: string;
  email?: string;
  phone?: string;
  hotelName?: string;
  roomNumber?: string;
  flightNumber?: string;
  arrivalDate?: string;
  departureDate?: string;
  status?: string;
  inboundStatus?: number;
  outboundStatus?: number;
  carClass?: string;
  vehicleId?: string;
  driverName?: string;
  isCritical?: boolean;
  isVip?: boolean;
  registrationTypeId?: string;
  registrationTypeName?: string;
  // Extended fields returned by the API
  rankValue?: string;
  designation?: string;
  photoUrl?: string;
  activeVehiclePlate?: string;
  activeVehicleId?: string;
  deservedCarClassName?: string;
  deservedCarClassColor?: string;
  deservedCarClassId?: string;
  statusLabel?: string;
  inboundStatusLabel?: string;
  outboundStatusLabel?: string;
}

export interface Guest extends GuestSummary {
  checklistItems?: ChecklistItem[];
  statusHistory?: GuestStatusHistoryItem[];
  notes?: string;
}

export interface ChecklistItem {
  id: string;
  name: string;
  isCompleted: boolean;
  completedAt?: string;
  completedByName?: string;
  notes?: string;
}

export interface GuestStatusHistoryItem {
  id: string;
  status: string;
  track: 'Inbound' | 'Outbound';
  notes?: string;
  changedAt: string;
  changedByName?: string;
  changedByRole?: string;
}

export interface ArrivalFlightGroup {
  flightNumber: string;
  arrivalTime: string;
  guests: GuestSummary[];
}

export interface Vehicle {
  id: string;
  plateNumber: string;
  model?: string;
  carClass?: string;
  driverName?: string;
  driverPhone?: string;
  isAvailable: boolean;
}

export interface VehicleWithStatus {
  id: string;
  licensePlate: string;
  make: string;
  model: string;
  color?: string;
  status?: string;
  driverName?: string;
  driverPhone?: string;
  currentGuestId?: string;
  currentGuestName?: string;
  carClassId?: string;
  carClassName?: string;
  carClassColor?: string;
  carNumber?: string;
  driverId?: string;
}

export interface Alert {
  id: string;
  title: string;
  message: string;
  severity: AlertSeverity;
  isResolved: boolean;
  createdAt: string;
  resolvedAt?: string;
  guestId?: string;
  guestName?: string;
  eventKey?: string;
}

export interface DashboardSummary {
  totalGuests: number;
  atAirport: number;
  inTransit: number;
  atHotel: number;
  checkedOut?: number;
  departed?: number;
  vehiclesAvailable: number;
  vehiclesAssigned: number;
  driversAvailable: number;
  guestsWithoutVehicle: number;
  guestsDeservingCarClass: number;
  activeAlerts: Alert[];
  recentActivity: ActivityItem[];
  fleetByClass?: FleetClassSummary[];
}

export interface ActivityItem {
  id: string;
  guestName: string;
  action: string;
  detail?: string;
  timestamp: string;
  actorName?: string;
}

export interface FleetClassSummary {
  className: string;
  available: number;
  assigned: number;
  guests: number;
  total: number;
}

export interface CreateDepartureRequest {
  guestId: string;
  flightNumber: string;
  departureDate: string;
  notes?: string;
}

export interface PagePermission {
  role: string;
  pageId: string;
  isGranted: boolean;
}
