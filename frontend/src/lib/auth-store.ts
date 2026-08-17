import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface Customer {
  id: string
  email: string
  fullName: string
  role: string
  createdAt: string
}

export interface AuthState {
  token: string | null
  customer: Customer | null
  login: (data: { token: string; customer: Customer }) => void
  logout: () => void
  isAuthenticated: () => boolean
  isAdmin: () => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      customer: null,

      login: (data) => set({ token: data.token, customer: data.customer }),

      logout: () => set({ token: null, customer: null }),

      isAuthenticated: () => get().token !== null && get().customer !== null,

      isAdmin: () => get().customer?.role === 'Admin',
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({ token: state.token, customer: state.customer }),
    },
  ),
)
