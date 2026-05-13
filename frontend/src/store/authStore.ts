import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { StaffUser } from '../types'
import { UserRole } from '../types'

interface AuthState {
  user: StaffUser | null
  accessToken: string | null
  isAuthenticated: boolean
  /** Whether the current user is an Admin (has access to all pages). */
  isAdmin: boolean
  /** Kept for compatibility but no longer used for access control. */
  grantedPageIds: string[]

  login: (user: StaffUser, accessToken: string, refreshToken: string) => void
  logout: () => void
  setPermissions: (isAdmin: boolean, grantedPageIds: string[]) => void
  /** Returns true if the current user can access the given pageId.
   *  Access control feature was removed - always returns true.
   *  Role-based visibility is handled by ProtectedRoute and AppLayout role checks. */
  hasPage: (pageId: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, _get) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      isAdmin: false,
      grantedPageIds: [],

      login: (user, accessToken, refreshToken) => {
        localStorage.setItem('accessToken', accessToken)
        localStorage.setItem('refreshToken', refreshToken)
        // Derive isAdmin from user role directly - no API call needed
        const roles: UserRole[] = user.roles?.length ? user.roles : (user.role ? [user.role] : [])
        const isAdmin = roles.includes(UserRole.Admin)
        set({ user, accessToken, isAuthenticated: true, isAdmin })
      },

      logout: () => {
        localStorage.removeItem('accessToken')
        localStorage.removeItem('refreshToken')
        set({ user: null, accessToken: null, isAuthenticated: false, isAdmin: false, grantedPageIds: [] })
      },

      setPermissions: (isAdmin, grantedPageIds) => {
        set({ isAdmin, grantedPageIds })
      },

      hasPage: (_pageId: string) => {
        // Access control feature removed - all authenticated users can see all nav items
        // Role-based access is enforced by ProtectedRoute wrappers in App.tsx
        return true
      },
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({
        user: state.user,
        accessToken: state.accessToken,
        isAuthenticated: state.isAuthenticated,
        isAdmin: state.isAdmin,
        grantedPageIds: state.grantedPageIds,
      }),
    }
  )
)
