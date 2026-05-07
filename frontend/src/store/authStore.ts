import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { StaffUser } from '../types'

interface AuthState {
  user: StaffUser | null
  accessToken: string | null
  isAuthenticated: boolean
  /** Whether the current user is an Admin (has access to all pages). */
  isAdmin: boolean
  /** List of pageIds the current user is permitted to access. Empty if isAdmin=true. */
  grantedPageIds: string[]

  login: (user: StaffUser, accessToken: string, refreshToken: string) => void
  logout: () => void
  setPermissions: (isAdmin: boolean, grantedPageIds: string[]) => void
  /** Returns true if the current user can access the given pageId. */
  hasPage: (pageId: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      isAdmin: false,
      grantedPageIds: [],

      login: (user, accessToken, refreshToken) => {
        localStorage.setItem('accessToken', accessToken)
        localStorage.setItem('refreshToken', refreshToken)
        set({ user, accessToken, isAuthenticated: true })
      },

      logout: () => {
        localStorage.removeItem('accessToken')
        localStorage.removeItem('refreshToken')
        set({ user: null, accessToken: null, isAuthenticated: false, isAdmin: false, grantedPageIds: [] })
      },

      setPermissions: (isAdmin, grantedPageIds) => {
        set({ isAdmin, grantedPageIds })
      },

      hasPage: (pageId: string) => {
        const { isAdmin, grantedPageIds } = get()
        if (isAdmin) return true
        return grantedPageIds.includes(pageId)
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
