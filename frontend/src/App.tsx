import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from './store/authStore'
import { UserRole } from './types'

// Pages
import LoginPage from './pages/auth/LoginPage'
import AirportDashboard from './pages/airport/AirportDashboard'
import GuestDetailPage from './pages/airport/GuestDetailPage'
import TransportDashboard from './pages/transport/TransportDashboard'
import TransportRoleDashboard from './pages/transport/TransportRoleDashboard'
import HotelDashboard from './pages/hotel/HotelDashboard'
import HotelDashboardPage from './pages/hotel/HotelDashboardPage'
import HotelArrivalsQueuePage from './pages/hotel/HotelArrivalsQueuePage'
import HotelGuestDirectoryPage from './pages/hotel/HotelGuestDirectoryPage'
import HotelGuestDetailPage from './pages/hotel/HotelGuestDetailPage'
import HotelManagementPage from './pages/hotel/HotelManagementPage'
import ControlRoomDashboard from './pages/controlroom/ControlRoomDashboard'
import OpsOverview from './pages/controlroom/OpsOverview'
import DepartureFormPage from './pages/departure/DepartureFormPage'
import DepartureManagePage from './pages/departure/DepartureManagePage'
import DepartureStatsPage from './pages/departure/DepartureStatsPage'
import AppLayout from './components/layout/AppLayout'
import EventsAirConfigPage from './pages/integrations/EventsAirConfigPage'
import FieldMappingsPage from './pages/integrations/FieldMappingsPage'
import InactiveParticipantsPage from './pages/data/InactiveParticipantsPage'
import SettingsPage from './pages/settings/SettingsPage'
import NotificationTemplatesPage from './pages/settings/NotificationTemplatesPage'
import NotificationHistoryPage from './pages/settings/NotificationHistoryPage'
import ChangePasswordPage from './pages/settings/ChangePasswordPage'
import PlacardPage from './pages/placard/PlacardPage'
import FleetPage from './pages/fleet/FleetPage'
import StaffManagementPage from './pages/staff/StaffManagementPage'
import CarClassManagementPage from './pages/fleet/CarClassManagementPage'
import GuestCarClassPage from './pages/fleet/GuestCarClassPage'
import ProfilePage from './pages/profile/ProfilePage'
import LiaisonDashboard from './pages/liaison/LiaisonDashboard'
import LiaisonGuestListPage from './pages/liaison/LiaisonGuestListPage'
import LiaisonGuestDetailPage from './pages/liaison/LiaisonGuestDetailPage'
import AccessControlPage from './pages/settings/AccessControlPage'

/** Multi-role aware ProtectedRoute */
function ProtectedRoute({ children, allowedRoles }: { children: React.ReactNode; allowedRoles?: UserRole[] }) {
  const { isAuthenticated, user } = useAuthStore()

  if (!isAuthenticated) return <Navigate to="/login" replace />

  if (allowedRoles && user) {
    const userRoles: UserRole[] = user.roles?.length ? user.roles : [user.role]
    const hasAccess = allowedRoles.some(r => userRoles.includes(r))
    if (!hasAccess) return <Navigate to="/unauthorized" replace />
  }

  return <>{children}</>
}

function RoleBasedHome() {
  const { user } = useAuthStore()
  if (!user) return <Navigate to="/login" replace />

  const userRoles: UserRole[] = user.roles?.length ? user.roles : [user.role]

  if (userRoles.includes(UserRole.Admin) || userRoles.includes(UserRole.ControlRoom)) {
    return <Navigate to="/control-room" replace />
  }
  if (userRoles.includes(UserRole.Airport)) {
    return <Navigate to="/airport" replace />
  }
  if (userRoles.includes(UserRole.Transport)) {
    return <Navigate to="/fleet" replace />
  }
  if (userRoles.includes(UserRole.Hotel)) {
    return <Navigate to="/hotel/dashboard" replace />
  }
  if (userRoles.includes(UserRole.Liaison)) {
    return <Navigate to="/liaison/dashboard" replace />
  }
  return <Navigate to="/login" replace />
}

const HOTEL_ROLES = [UserRole.Hotel, UserRole.ControlRoom, UserRole.Admin]

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/departure-request" element={<DepartureFormPage />} />
        <Route path="/departure" element={<DepartureFormPage />} />
        <Route path="/departure/manage/:token" element={<DepartureManagePage />} />

        {/* Placard — protected but outside AppLayout (full-screen, no nav) */}
        <Route path="/placard/:id" element={<ProtectedRoute><PlacardPage /></ProtectedRoute>} />

        {/* Protected routes */}
        <Route path="/" element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
          <Route index element={<RoleBasedHome />} />

          {/* Airport Module */}
          <Route path="airport" element={
            <ProtectedRoute allowedRoles={[UserRole.Airport, UserRole.ControlRoom, UserRole.Admin]}>
              <AirportDashboard />
            </ProtectedRoute>
          } />
          <Route path="airport/guest/:id" element={
            <ProtectedRoute allowedRoles={[UserRole.Airport, UserRole.Transport, UserRole.ControlRoom, UserRole.Admin]}>
              <GuestDetailPage />
            </ProtectedRoute>
          } />

          {/* Car Class Management — Admin Only (must be before fleet/*) */}
          <Route path="fleet/car-classes" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <CarClassManagementPage />
            </ProtectedRoute>
          } />

          {/* Guest Car Class Assignment — Admin Only (must be before fleet/*) */}
          <Route path="fleet/guest-car-class" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <GuestCarClassPage />
            </ProtectedRoute>
          } />

          {/* Fleet Module (Transport + Admin) — wildcard must be AFTER specific fleet routes */}
          <Route path="fleet/*" element={
            <ProtectedRoute allowedRoles={[UserRole.Transport, UserRole.Admin]}>
              <FleetPage />
            </ProtectedRoute>
          } />

          {/* Transport Module */}
          <Route path="transport/dashboard" element={
            <ProtectedRoute allowedRoles={[UserRole.Transport, UserRole.ControlRoom, UserRole.Admin]}>
              <TransportRoleDashboard />
            </ProtectedRoute>
          } />
          <Route path="transport" element={
            <ProtectedRoute allowedRoles={[UserRole.Transport, UserRole.ControlRoom, UserRole.Admin]}>
              <TransportDashboard />
            </ProtectedRoute>
          } />

          {/* ── Hotel Module (4 pages) ── */}
          {/* Legacy /hotel redirect → /hotel/dashboard */}
          <Route path="hotel" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <Navigate to="/hotel/dashboard" replace />
            </ProtectedRoute>
          } />
          <Route path="hotel/dashboard" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <HotelDashboardPage />
            </ProtectedRoute>
          } />
          <Route path="hotel/arrivals" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <HotelArrivalsQueuePage />
            </ProtectedRoute>
          } />
          <Route path="hotel/guests" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <HotelGuestDirectoryPage />
            </ProtectedRoute>
          } />
          <Route path="hotel/guest/:id" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <HotelGuestDetailPage />
            </ProtectedRoute>
          } />
          <Route path="hotel/management" element={
            <ProtectedRoute allowedRoles={HOTEL_ROLES}>
              <HotelManagementPage />
            </ProtectedRoute>
          } />

          {/* Control Room */}
          <Route path="control-room" element={
            <ProtectedRoute allowedRoles={[UserRole.ControlRoom, UserRole.Admin]}>
              <ControlRoomDashboard />
            </ProtectedRoute>
          } />
          <Route path="control-room/ops" element={
            <ProtectedRoute allowedRoles={[UserRole.ControlRoom, UserRole.Admin]}>
              <OpsOverview />
            </ProtectedRoute>
          } />

          {/* ── Liaison Module (read-only) ── */}
          <Route path="liaison/dashboard" element={
            <ProtectedRoute allowedRoles={[UserRole.Liaison, UserRole.ControlRoom, UserRole.Admin]}>
              <LiaisonDashboard />
            </ProtectedRoute>
          } />
          <Route path="liaison/guests" element={
            <ProtectedRoute allowedRoles={[UserRole.Liaison, UserRole.Admin]}>
              <LiaisonGuestListPage />
            </ProtectedRoute>
          } />
          <Route path="liaison/guest/:id" element={
            <ProtectedRoute allowedRoles={[UserRole.Liaison, UserRole.Admin]}>
              <LiaisonGuestDetailPage />
            </ProtectedRoute>
          } />

          {/* Staff Management — Admin Only */}
          <Route path="staff" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <StaffManagementPage />
            </ProtectedRoute>
          } />

          {/* Integrations — Admin Only */}
          <Route path="integrations/eventsair" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <EventsAirConfigPage />
            </ProtectedRoute>
          } />
          <Route path="integrations/field-mappings" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <FieldMappingsPage />
            </ProtectedRoute>
          } />

          {/* Data Management — Admin Only */}
          <Route path="data/inactive-participants" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <InactiveParticipantsPage />
            </ProtectedRoute>
          } />

          {/* Departure Shuttle Stats — Transport + Admin */}
          <Route path="departure-stats" element={
            <ProtectedRoute allowedRoles={[UserRole.Transport, UserRole.Admin, UserRole.ControlRoom]}>
              <DepartureStatsPage />
            </ProtectedRoute>
          } />

          {/* Access Control — Admin Only */}
          <Route path="access-control" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <AccessControlPage />
            </ProtectedRoute>
          } />

          {/* Settings — Admin Only */}
          <Route path="settings" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <SettingsPage />
            </ProtectedRoute>
          } />
          <Route path="notification-templates" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <NotificationTemplatesPage />
            </ProtectedRoute>
          } />
          <Route path="notification-history" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin]}>
              <NotificationHistoryPage />
            </ProtectedRoute>
          } />

          {/* Change Password — all authenticated roles */}
          <Route path="change-password" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin, UserRole.Airport, UserRole.Transport, UserRole.Hotel, UserRole.ControlRoom, UserRole.Liaison]}>
              <ChangePasswordPage />
            </ProtectedRoute>
          } />

          {/* Profile — All authenticated roles */}
          <Route path="profile" element={
            <ProtectedRoute allowedRoles={[UserRole.Admin, UserRole.Airport, UserRole.Transport, UserRole.Hotel, UserRole.ControlRoom, UserRole.Liaison]}>
              <ProfilePage />
            </ProtectedRoute>
          } />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
