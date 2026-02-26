import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import { UserRole } from '../../types'
import {
  Plane,
  Car,
  Building2,
  LayoutDashboard,
  LogOut,
  User,
  Bell,
  Plug,
  Settings,
  ListChecks,
} from 'lucide-react'

const navItems = [
  {
    to: '/airport',
    label: 'Airport',
    icon: Plane,
    roles: [UserRole.AirportReceptionSpecialist, UserRole.ControlRoom, UserRole.Administrator],
  },
  {
    to: '/transport',
    label: 'Transport',
    icon: Car,
    roles: [UserRole.TransportationSpecialist, UserRole.ControlRoom, UserRole.Administrator],
  },
  {
    to: '/hotel',
    label: 'Hotel',
    icon: Building2,
    roles: [UserRole.AccommodationSpecialist, UserRole.ControlRoom, UserRole.Administrator],
  },
  {
    to: '/control-room',
    label: 'Control Room',
    icon: LayoutDashboard,
    roles: [UserRole.ControlRoom, UserRole.Administrator],
  },
]

const adminNavItems = [
  {
    to: '/integrations/eventsair',
    label: 'EventsAir Config',
    icon: Plug,
    roles: [UserRole.Administrator],
  },
  {
    to: '/integrations/registration-types',
    label: 'Registration Types',
    icon: ListChecks,
    roles: [UserRole.Administrator],
  },
]

const adminSettingsItems = [
  {
    to: '/settings',
    label: 'Platform Settings',
    icon: Settings,
    roles: [UserRole.Administrator],
  },
]

export default function AppLayout() {
  const { user, logout } = useAuthStore()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  const visibleNav = navItems.filter(
    (item) => user && item.roles.includes(user.role)
  )

  return (
    <div className="flex h-screen bg-gray-50">
      {/* Sidebar */}
      <aside className="w-64 bg-white border-r border-gray-200 flex flex-col shadow-sm">
        {/* Logo */}
        <div className="p-5 border-b border-gray-100">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-isdb-green flex items-center justify-center">
              <span className="text-white font-bold text-sm">IsDB</span>
            </div>
            <div>
              <p className="font-semibold text-gray-900 text-sm">Hospitality</p>
              <p className="text-xs text-gray-500">Guest Management</p>
            </div>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
          {visibleNav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-isdb-green text-white'
                    : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
                }`
              }
            >
              <item.icon className="w-4 h-4" />
              {item.label}
            </NavLink>
          ))}

          {/* Admin-only: Integrations section */}
          {user?.role === UserRole.Administrator && (
            <>
              <div className="pt-3 pb-1">
                <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Integrations</p>
              </div>
              {adminNavItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-isdb-green text-white'
                        : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
                    }`
                  }
                >
                  <item.icon className="w-4 h-4" />
                  {item.label}
                </NavLink>
              ))}
              <div className="pt-3 pb-1">
                <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Administration</p>
              </div>
              {adminSettingsItems.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-isdb-green text-white'
                        : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
                    }`
                  }
                >
                  <item.icon className="w-4 h-4" />
                  {item.label}
                </NavLink>
              ))}
            </>
          )}
        </nav>

        {/* User footer */}
        <div className="p-3 border-t border-gray-100">
          <div className="flex items-center gap-3 px-3 py-2 rounded-lg bg-gray-50 mb-1">
            <div className="w-8 h-8 rounded-full bg-isdb-green/10 flex items-center justify-center">
              <User className="w-4 h-4 text-isdb-green" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">{user?.fullName}</p>
              <p className="text-xs text-gray-500 truncate">{user?.role.replace(/([A-Z])/g, ' $1').trim()}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 w-full px-3 py-2 text-sm text-gray-600 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
          >
            <LogOut className="w-4 h-4" />
            Sign out
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
