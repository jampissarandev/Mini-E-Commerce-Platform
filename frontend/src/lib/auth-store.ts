import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface User {
  id: string
  email: string
  fullName: string
  role: string
  createdAt: string
}

export interface AuthState {
  token: string | null
  user: User | null
  login: (data: { token: string; user: User }) => void
  logout: () => void
  isAuthenticated: () => boolean
  isAdmin: () => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      user: null,

      login: (data) => set({ token: data.token, user: data.user }),

      logout: () => set({ token: null, user: null }),

      isAuthenticated: () => get().token !== null && get().user !== null,

      isAdmin: () => get().user?.role === 'Admin',
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({ token: state.token, user: state.user }),
    },
  ),
)
