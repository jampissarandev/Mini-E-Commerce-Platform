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
// We use a CustomEvent so this module stays decoupled from React Router.
let isRefreshing = false
let pendingQueue: Array<(token: string | null) => void> = []

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config as (typeof error.config & { _retry?: boolean }) | undefined
    const status = error.response?.status as number | undefined
    const url: string = (original?.url as string) ?? ''

    // Don't intercept refresh/logout themselves to avoid loops
    const isAuthRefresh = url.includes('/auth/refresh') || url.includes('/auth/logout')

    if (status === 401 && original && !original._retry && !isAuthRefresh && useAuthStore.getState().isAuthenticated()) {
      if (isRefreshing) {
        // Queue until the in-flight refresh resolves (per ADR 0005 cross-tab race is first-wins)
        return new Promise((resolve, reject) => {
          pendingQueue.push((newToken) => {
            if (newToken && original.headers) {
              (original.headers as Record<string, string>).Authorization = `Bearer ${newToken}`
            }
            // Re-dispatch through axios so interceptors run again
            if (newToken) resolve(api(original))
            else reject(error)
          })
        })
      }

      original._retry = true
      isRefreshing = true
      try {
        const { data } = await api.post('/auth/refresh')
        const newToken = (data?.data?.token ?? data?.token) as string | undefined
        if (newToken) {
          const customer = (data?.data?.customer ?? data?.customer) as { id: string; email: string; fullName: string; role: string; createdAt: string } | undefined
          if (customer) useAuthStore.getState().login({ token: newToken, customer })
          else {
            // Keep existing customer but update token
            const prev = useAuthStore.getState().customer
            if (prev) useAuthStore.getState().login({ token: newToken, customer: prev })
          }
          pendingQueue.forEach((cb) => cb(newToken))
          pendingQueue = []
          if (original.headers) (original.headers as Record<string, string>).Authorization = `Bearer ${newToken}`
          return api(original)
        }
      } catch {
        // refresh failed — fall through to logout
      } finally {
        isRefreshing = false
      }
      pendingQueue.forEach((cb) => cb(null))
      pendingQueue = []
      useAuthStore.getState().logout()
      if (typeof window !== 'undefined') window.dispatchEvent(new CustomEvent('auth:unauthorized'))
      return Promise.reject(error)
    }

    return Promise.reject(error)
  },
)
