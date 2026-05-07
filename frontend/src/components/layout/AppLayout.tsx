import { useState } from 'react'
import { Outlet, NavLink, useNavigate, Link } from 'react-router-dom'
import NotificationBell from '../notifications/NotificationBell'
import { useAuthStore } from '../../store/authStore'
import { UserRole } from '../../types'
import {
  Plane,
  Car,
  Building2,
  LayoutDashboard,
  LogOut,
  User,
  Plug,
  Settings,
  Sliders,
  Truck,
  Users,
  Tag,
  Bus,
  Bell,
  History,
  ChevronRight,
  Menu,
  X,
  Eye,
  ConciergeBell,
  ListChecks,
  Hotel,
  Shield,
} from 'lucide-react'

// ─── Nav item type ─────────────────────────────────────────────────────────────
type NavItem = {
  to: string
  label: string
  icon: React.ElementType
  /** pageId used for RBAC check. If undefined, item is always visible (admin-only sections). */
  pageId?: string
  /** Fallback role-based visibility for items that have no pageId (admin-only). */
  roles?: UserRole[]
  end?: boolean
}

// ─── Page definitions with pageIds ────────────────────────────────────────────
const mainNavItems: NavItem[] = [
  { to: '/airport',         label: 'Airport',           icon: Plane,          pageId: 'airport.dashboard' },
  { to: '/transport',       label: 'Transport',          icon: Car,            pageId: 'transport.dashboard' },
  { to: '/departure-stats', label: 'Departure Shuttle',  icon: Bus,            pageId: 'transport.departure_stats' },
  { to: '/control-room',    label: 'Control Room',       icon: LayoutDashboard, pageId: 'controlroom.dashboard' },
]

const hotelNavItems: NavItem[] = [
  { to: '/hotel/dashboard',   label: 'Dashboard',        icon: LayoutDashboard, pageId: 'hotel.dashboard' },
  { to: '/hotel/arrivals',    label: 'Arrivals Queue',   icon: ConciergeBell,   pageId: 'hotel.arrivals' },
  { to: '/hotel/guests',      label: 'Guest Directory',  icon: Users,           pageId: 'hotel.guests' },
  { to: '/hotel/management',  label: 'Hotel Management', icon: Hotel,           pageId: 'hotel.management' },
]

const liaisonNavItems: NavItem[] = [
  { to: '/liaison/dashboard', label: 'Dashboard',        icon: LayoutDashboard, pageId: 'liaison.dashboard' },
  { to: '/liaison/guests',    label: 'Guest Directory',  icon: Users,           pageId: 'liaison.guests' },
]

// Fleet items are not in the RBAC matrix (data management, admin-only)
const dataNavItems: NavItem[] = [
  { to: '/fleet',               label: 'Fleet',                     icon: Truck, roles: [UserRole.Transport, UserRole.Admin], end: true },
  { to: '/fleet/car-classes',   label: 'Car Classes',               icon: Tag,   roles: [UserRole.Admin] },
  { to: '/fleet/guest-car-class', label: 'Guest Car Class Assignment', icon: Car, roles: [UserRole.Admin] },
]

const adminNavItems: NavItem[] = [
  { to: '/staff',                     label: 'Staff Management',      icon: Users,    roles: [UserRole.Admin] },
  { to: '/integrations/eventsair',    label: 'EventsAir Config',      icon: Plug,     roles: [UserRole.Admin] },
  { to: '/integrations/field-mappings', label: 'Field Mappings',      icon: Sliders,  roles: [UserRole.Admin] },
  { to: '/access-control',            label: 'Access Control',        icon: Shield,   roles: [UserRole.Admin] },
]

const adminSettingsItems: NavItem[] = [
  { to: '/settings',              label: 'Platform Settings',     icon: Settings, roles: [UserRole.Admin] },
  { to: '/notification-templates', label: 'Notification Templates', icon: Bell,   roles: [UserRole.Admin] },
  { to: '/notification-history',  label: 'Notification History',  icon: History,  roles: [UserRole.Admin] },
]

/** Check if user has any of the required roles (multi-role aware) */
function hasRole(userRoles: UserRole[], requiredRoles: UserRole[]): boolean {
  return requiredRoles.some(r => userRoles.includes(r))
}

interface SidebarContentProps {
  userRoles: UserRole[]
  isAdmin: boolean
  isLiaison: boolean
  isHotel: boolean
  user: { fullName: string } | null
  roleLabel: string
  onNavClick: () => void
  handleLogout: () => void
  hasPage: (pageId: string) => boolean
}

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors ${
    isActive ? 'bg-isdb-green text-white' : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
  }`

function SidebarContent({ userRoles, isAdmin, isLiaison, isHotel, user, roleLabel, onNavClick, handleLogout, hasPage }: SidebarContentProps) {
  // Filter main nav items by pageId permission
  const visibleMain = mainNavItems.filter(item => item.pageId ? hasPage(item.pageId) : (item.roles ? hasRole(userRoles, item.roles) : false))
  const visibleHotel = hotelNavItems.filter(item => item.pageId ? hasPage(item.pageId) : false)
  const visibleLiaison = liaisonNavItems.filter(item => item.pageId ? hasPage(item.pageId) : false)

  const showHotelSection = visibleHotel.length > 0
  const showLiaisonSection = visibleLiaison.length > 0

  return (
    <>
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
        {/* Main nav items */}
        {visibleMain.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} onClick={onNavClick} className={navLinkClass}>
            <item.icon className="w-4 h-4" />
            {item.label}
          </NavLink>
        ))}

        {/* Hotel section */}
        {showHotelSection && (
          <>
            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Hotel</p>
            </div>
            {visibleHotel.map((item) => (
              <NavLink key={item.to} to={item.to} onClick={onNavClick} className={navLinkClass}>
                <item.icon className="w-4 h-4" />
                {item.label}
              </NavLink>
            ))}
          </>
        )}

        {/* Liaison section */}
        {showLiaisonSection && (
          <>
            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">
                {isAdmin ? 'Liaison View' : 'Liaison'}
              </p>
            </div>
            {visibleLiaison.map((item) => (
              <NavLink key={item.to} to={item.to} onClick={onNavClick} className={navLinkClass}>
                {isAdmin ? <Eye className="w-4 h-4" /> : <item.icon className="w-4 h-4" />}
                {item.label}
              </NavLink>
            ))}
          </>
        )}

        {/* Admin-only sections */}
        {isAdmin && (
          <>
            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Administration</p>
            </div>
            {adminNavItems.map((item) => (
              <NavLink key={item.to} to={item.to} onClick={onNavClick} className={navLinkClass}>
                <item.icon className="w-4 h-4" />
                {item.label}
              </NavLink>
            ))}

            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Data Management</p>
            </div>
            {dataNavItems.filter(item => item.roles ? hasRole(userRoles, item.roles) : false).map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} onClick={onNavClick} className={navLinkClass}>
                <item.icon className="w-4 h-4" />
                {item.label}
              </NavLink>
            ))}

            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Settings</p>
            </div>
            {adminSettingsItems.map((item) => (
              <NavLink key={item.to} to={item.to} onClick={onNavClick} className={navLinkClass}>
                <item.icon className="w-4 h-4" />
                {item.label}
              </NavLink>
            ))}
          </>
        )}

        {/* Transport role: fleet access */}
        {!isAdmin && hasRole(userRoles, [UserRole.Transport]) && (
          <>
            <div className="pt-3 pb-1">
              <p className="px-3 text-xs font-semibold text-gray-400 uppercase tracking-wider">Data Management</p>
            </div>
            <NavLink to="/fleet" end onClick={onNavClick} className={navLinkClass}>
              <Truck className="w-4 h-4" />
              Fleet
            </NavLink>
          </>
        )}
      </nav>

      {/* User footer */}
      <div className="p-3 border-t border-gray-100">
        <Link
          to="/profile"
          onClick={onNavClick}
          className="flex items-center gap-3 px-3 py-2 rounded-lg bg-gray-50 hover:bg-isdb-green/5 hover:border-isdb-green/20 border border-transparent transition-colors mb-1 group"
        >
          <div className="w-8 h-8 rounded-full bg-isdb-green/10 flex items-center justify-center flex-shrink-0">
            <User className="w-4 h-4 text-isdb-green" />
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-gray-900 truncate">{user?.fullName}</p>
            <p className="text-xs text-gray-500 truncate">{roleLabel}</p>
          </div>
          <ChevronRight className="w-3.5 h-3.5 text-gray-400 group-hover:text-isdb-green flex-shrink-0" />
        </Link>
        <button
          onClick={handleLogout}
          className="flex items-center gap-2 w-full px-3 py-2 text-sm text-gray-600 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors"
        >
          <LogOut className="w-4 h-4" />
          Sign out
        </button>
      </div>
    </>
  )
}

export default function AppLayout() {
  const { user, logout, isAdmin, hasPage } = useAuthStore()
  const navigate = useNavigate()
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const handleLogout = () => {
    logout()
    navigate('/login')
  }

  // Normalise: use roles array if available, fall back to single role
  const userRoles: UserRole[] = user
    ? (user.roles?.length ? user.roles : [user.role])
    : []

  const isLiaison = userRoles.includes(UserRole.Liaison) && !isAdmin
  const isHotel = userRoles.includes(UserRole.Hotel) && !isAdmin

  const roleLabel = userRoles
    .map(r => r.replace(/([A-Z])/g, ' $1').trim())
    .join(', ')

  const closeSidebar = () => setSidebarOpen(false)

  return (
    <div className="flex h-screen bg-gray-50">
      {/* ── Desktop sidebar (hidden on mobile) ── */}
      <aside className="hidden md:flex w-64 bg-white border-r border-gray-200 flex-col shadow-sm flex-shrink-0">
        <SidebarContent
          userRoles={userRoles}
          isAdmin={isAdmin}
          isLiaison={isLiaison}
          isHotel={isHotel}
          user={user}
          roleLabel={roleLabel}
          onNavClick={() => {}}
          handleLogout={handleLogout}
          hasPage={hasPage}
        />
      </aside>

      {/* ── Mobile sidebar drawer overlay ── */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 md:hidden"
          onClick={closeSidebar}
          aria-hidden="true"
        />
      )}

      {/* ── Mobile sidebar drawer panel ── */}
      <aside
        className={`fixed inset-y-0 left-0 z-50 w-72 bg-white flex flex-col shadow-xl transform transition-transform duration-300 ease-in-out md:hidden ${
          sidebarOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Close button inside drawer */}
        <button
          onClick={closeSidebar}
          className="absolute top-4 right-4 p-1.5 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
          aria-label="Close menu"
        >
          <X className="w-5 h-5" />
        </button>
        <SidebarContent
          userRoles={userRoles}
          isAdmin={isAdmin}
          isLiaison={isLiaison}
          isHotel={isHotel}
          user={user}
          roleLabel={roleLabel}
          onNavClick={closeSidebar}
          handleLogout={handleLogout}
          hasPage={hasPage}
        />
      </aside>

      {/* ── Main content ── */}
      <main className="flex-1 overflow-auto flex flex-col min-w-0">
        {/* Top bar */}
        <div className="flex items-center justify-between px-4 py-2 bg-white border-b border-gray-100 shadow-sm flex-shrink-0">
          {/* Hamburger — mobile only */}
          <button
            onClick={() => setSidebarOpen(true)}
            className="md:hidden p-2 rounded-lg text-gray-500 hover:bg-gray-100 transition-colors"
            aria-label="Open menu"
          >
            <Menu className="w-5 h-5" />
          </button>
          <div className="flex-1" />
          <NotificationBell />
        </div>

        {/* Page content */}
        <div className="flex-1 overflow-auto">
          <Outlet />
        </div>
      </main>
    </div>
  )
}
