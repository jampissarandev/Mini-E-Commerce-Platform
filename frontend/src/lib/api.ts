import axios from 'axios'
import { useAuthStore } from '@/lib/auth-store'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true,
})

// Attach JWT token to every request
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// On 401, try silent refresh once before logging out (ADR 0005).
// Per spec: "the original request is retried once" (singular). Concurrent
// in-flight 401s from the same tab are NOT coalesced here — ADR 0005
// explicitly rejected per-tab frontend coalescing ("only works inside a
// single tab"). The first request to 401 refreshes; subsequent 401s in the
// same tab see the new token (zustand update is synchronous) and the
// re-request naturally carries it. Cross-tab races are first-wins at the
// server (conditional UPDATE per ADR 0005).
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config as (typeof error.config & { _retry?: boolean }) | undefined
    const status = error.response?.status as number | undefined
    const url: string = (original?.url as string) ?? ''

    // Don't intercept refresh/logout themselves to avoid loops
    const isAuthEndpoint = url.includes('/auth/refresh') || url.includes('/auth/logout')

    if (
      status === 401 &&
      original &&
      !original._retry &&
      !isAuthEndpoint &&
      useAuthStore.getState().isAuthenticated()
    ) {
      original._retry = true
      try {
        const { data } = await api.post('/auth/refresh')
        const newToken = (data?.data?.token ?? data?.token) as string | undefined
        if (newToken) {
          const customer =
            (data?.data?.customer ?? data?.customer) ??
            useAuthStore.getState().customer
          if (customer) {
            useAuthStore.getState().login({ token: newToken, customer })
          }
          if (original.headers) {
            (original.headers as Record<string, string>).Authorization = `Bearer ${newToken}`
          }
          return api(original)
        }
      } catch {
        // refresh failed — fall through to logout
      }
      useAuthStore.getState().logout()
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('auth:unauthorized'))
      }
      return Promise.reject(error)
    }

    return Promise.reject(error)
  },
)
