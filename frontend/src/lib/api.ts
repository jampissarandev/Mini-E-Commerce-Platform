import axios from 'axios'
import { useAuthStore } from '@/lib/auth-store'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Attach JWT token to every request
api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// On 401, clear auth state and notify the app so it can redirect to /login.
// We use a CustomEvent (rather than calling navigate() directly) so this
// module stays decoupled from React Router — the listener lives in main.tsx.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const wasAuthenticated = useAuthStore.getState().isAuthenticated()
      useAuthStore.getState().logout()
      // Only redirect if the user was previously authenticated; otherwise
      // the route guard already handles the unauthenticated case and we'd
      // be fighting it (e.g. on the /login page itself).
      if (wasAuthenticated && typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('auth:unauthorized'))
      }
    }
    return Promise.reject(error)
  },
)
