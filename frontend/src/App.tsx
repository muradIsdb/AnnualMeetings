import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from './store/authStore'
import { UserRole } from './types'

// Pages
import LoginPage from './pages/auth/LoginPage'
import AirportDashboard from './pages/airport/AirportDashboard'
import GuestDetailPage from './pages/airport/GuestDetailPage'
import TransportDashboard from './pages/transport/TransportDashboard'
import HotelDashboard from './pages/hotel/HotelDashboard'
import ControlRoomDashboard from './pages/controlroom/ControlRoomDashboard'
import DepartureFormPage from './pages/departure/DepartureFormPage'
import AppLayout from './components/layout/AppLayout'
import EventsAirConfigPage from './pages/integrations/EventsAirConfigPage'
import RegistrationTypesPage from './pages/integrations/RegistrationTypesPage'
import FieldMappingsPage from './pages/integrations/FieldMappingsPage'
import InactiveParticipantsPage from './pages/data/InactiveParticipantsPage'
import SettingsPage from './pages/settings/SettingsPage'

function ProtectedRoute({ children, allowedRoles }: { children: React.ReactNode; allowedRoles?: UserRole[] }) {
  const { isAuthenticated, user } = useAuthStore()

  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to="/unauthorized" replace />
  }
  return <>{children}</>
}

function RoleBasedHome() {
  const { user } = useAuthStore()
  if (!user) return <Navigate to="/login" replace />

  switch (user.role) {
    case UserRole.AirportReceptionSpecialist:
      return <Navigate to="/airport" replace />
    case UserRole.TransportationSpecialist:
      return <Navigate to="/transport" replace />
    case UserRole.AccommodationSpecialist:
      return <Navigate to="/hotel" replace />
    case UserRole.ControlRoom:
    case UserRole.Administrator:
      return <Navigate to="/control-room" replace />
    default:
      return <Navigate to="/login" replace />
  }
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/departure-request" element={<DepartureFormPage />} />

        {/* Protected routes */}
        <Route path="/" element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
          <Route index element={<RoleBasedHome />} />

          {/* Airport Module */}
          <Route path="airport" element={
            <ProtectedRoute allowedRoles={[UserRole.AirportReceptionSpecialist, UserRole.ControlRoom, UserRole.Administrator]}>
              <AirportDashboard />
            </ProtectedRoute>
          } />
          <Route path="airport/guest/:id" element={
            <ProtectedRoute allowedRoles={[UserRole.AirportReceptionSpecialist, UserRole.ControlRoom, UserRole.Administrator]}>
              <GuestDetailPage />
            </ProtectedRoute>
          } />

          {/* Transport Module */}
          <Route path="transport" element={
            <ProtectedRoute allowedRoles={[UserRole.TransportationSpecialist, UserRole.ControlRoom, UserRole.Administrator]}>
              <TransportDashboard />
            </ProtectedRoute>
          } />

          {/* Hotel Module */}
          <Route path="hotel" element={
            <ProtectedRoute allowedRoles={[UserRole.AccommodationSpecialist, UserRole.ControlRoom, UserRole.Administrator]}>
              <HotelDashboard />
            </ProtectedRoute>
          } />

          {/* Control Room */}
          <Route path="control-room" element={
            <ProtectedRoute allowedRoles={[UserRole.ControlRoom, UserRole.Administrator]}>
              <ControlRoomDashboard />
            </ProtectedRoute>
          } />

          {/* Integrations — Admin Only */}
          <Route path="integrations/eventsair" element={
            <ProtectedRoute allowedRoles={[UserRole.Administrator]}>
              <EventsAirConfigPage />
            </ProtectedRoute>
          } />

          <Route path="integrations/registration-types" element={
            <ProtectedRoute allowedRoles={[UserRole.Administrator]}>
              <RegistrationTypesPage />
            </ProtectedRoute>
          } />

          <Route path="integrations/field-mappings" element={
            <ProtectedRoute allowedRoles={[UserRole.Administrator]}>
              <FieldMappingsPage />
            </ProtectedRoute>
          } />

          {/* Data Management — Admin Only */}
          <Route path="data/inactive-participants" element={
            <ProtectedRoute allowedRoles={[UserRole.Administrator]}>
              <InactiveParticipantsPage />
            </ProtectedRoute>
          } />

          {/* Settings — Admin Only */}
          <Route path="settings" element={
            <ProtectedRoute allowedRoles={[UserRole.Administrator]}>
              <SettingsPage />
            </ProtectedRoute>
          } />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
