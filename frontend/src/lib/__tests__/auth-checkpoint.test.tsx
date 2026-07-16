/**
 * Auth checkpoint integration tests.
 *
 * Covers:
 *   - Token persists across page reload (Zustand persist → localStorage)
 *   - 401 from API triggers automatic logout + redirect to /login
 */

import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { useAuthStore } from '@/lib/auth-store'
import { api } from '@/lib/api'
import { ProtectedRoute } from '@/components/ProtectedRoute'

// ─── helpers ──────────────────────────────────────────────────────────

const AUTH_KEY = 'auth-storage'

function renderWithRouter(initialEntries: string[]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <Routes>
          <Route path="/" element={<div>Home</div>} />
          <Route path="/login" element={<div>Login Page</div>} />
          <Route
            path="/protected"
            element={
              <ProtectedRoute>
                <div>Protected Content</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, customer: null })
})

// ─── Token persistence ────────────────────────────────────────────────

describe('Token persists across reload', () => {
  it('stores token in localStorage after login', async () => {
    server.use(
      http.post('**/api/auth/login', () =>
        HttpResponse.json({
          success: true,
          data: {
            token: 'persist-me-token',
            expiresAt: '2026-12-31T23:59:59Z',
            customer: {
              id: '1',
              email: 'alice@example.com',
              fullName: 'Alice',
              role: 'Customer',
              createdAt: '2026-01-01T00:00:00Z',
            },
          },
        }),
      ),
    )

    const { unmount } = renderWithRouter(['/login'])

    // Simulate login
    useAuthStore.getState().login({
      token: 'persist-me-token',
      customer: {
        id: '1',
        email: 'alice@example.com',
        fullName: 'Alice',
        role: 'Customer',
        createdAt: '2026-01-01T00:00:00Z',
      },
    })

    // Verify localStorage was written
    await waitFor(() => {
      const stored = localStorage.getItem(AUTH_KEY)
      expect(stored).toBeTruthy()
      expect(stored).toContain('persist-me-token')
      expect(stored).toContain('alice@example.com')
    })

    unmount()

    // Simulate "reload" — read from localStorage, rehydrate store
    const raw = localStorage.getItem(AUTH_KEY)
    expect(raw).toBeTruthy()

    // Rehydrate the store as Zustand persist would on page load
    const parsed = JSON.parse(raw!)
    useAuthStore.setState({
      token: parsed.state.token,
      customer: parsed.state.customer,
    })

    // Verify state survived the "reload"
    expect(useAuthStore.getState().token).toBe('persist-me-token')
    expect(useAuthStore.getState().customer?.email).toBe('alice@example.com')
    expect(useAuthStore.getState().isAuthenticated()).toBe(true)
  })

  it('rehydrates admin role from localStorage', () => {
    // Seed localStorage as if a previous session stored it
    const persistedState = {
      state: {
        token: 'admin-token',
        customer: {
          id: '2',
          email: 'admin@example.com',
          fullName: 'Admin User',
          role: 'Admin',
          createdAt: '2026-01-01T00:00:00Z',
        },
      },
      version: 0,
    }
    localStorage.setItem(AUTH_KEY, JSON.stringify(persistedState))

    // Rehydrate — this is what Zustand persist does on app init
    const parsed = JSON.parse(localStorage.getItem(AUTH_KEY)!)
    useAuthStore.setState({
      token: parsed.state.token,
      customer: parsed.state.customer,
    })

    expect(useAuthStore.getState().isAdmin()).toBe(true)
    expect(useAuthStore.getState().isAuthenticated()).toBe(true)
  })
})

// ─── 401 auto-logout ──────────────────────────────────────────────────

describe('401 from API triggers logout + redirect', () => {
  it('clears auth state when API returns 401', async () => {
    // Pre-populate auth state as if user is logged in
    useAuthStore.setState({
      token: 'expired-token',
      customer: {
        id: '1',
        email: 'alice@example.com',
        fullName: 'Alice',
        role: 'Customer',
        createdAt: '2026-01-01T00:00:00Z',
      },
    })

    // Also persist it in localStorage to verify it gets cleared
    localStorage.setItem(
      AUTH_KEY,
      JSON.stringify({
        state: {
          token: 'expired-token',
          customer: {
            id: '1',
            email: 'alice@example.com',
            fullName: 'Alice',
            role: 'Customer',
            createdAt: '2026-01-01T00:00:00Z',
          },
        },
        version: 0,
      }),
    )

    expect(useAuthStore.getState().isAuthenticated()).toBe(true)

    // Stub any GET to return 401 — simulates an expired/invalid token
    server.use(
      http.get('**/api/**', () =>
        HttpResponse.json(
          {
            success: false,
            error: { code: 'UNAUTHORIZED', message: 'Unauthorized' },
          },
          { status: 401 },
        ),
      ),
    )

    // Make an API call that will return 401
    try {
      await api.get('/protected-resource')
    } catch {
      // Expected — 401 causes rejection
    }

    // The axios interceptor should have cleared auth state
    await waitFor(() => {
      expect(useAuthStore.getState().token).toBeNull()
      expect(useAuthStore.getState().customer).toBeNull()
      expect(useAuthStore.getState().isAuthenticated()).toBe(false)
    })
  })

  it('redirects to /login after 401 clears auth state', async () => {
    // Simulate being on a protected page while logged in
    useAuthStore.setState({
      token: 'expired-token',
      customer: {
        id: '1',
        email: 'alice@example.com',
        fullName: 'Alice',
        role: 'Customer',
        createdAt: '2026-01-01T00:00:00Z',
      },
    })

    renderWithRouter(['/protected'])

    // Verify protected content is shown (user is "authenticated")
    expect(screen.getByText('Protected Content')).toBeInTheDocument()

    // Now simulate a 401 that clears auth — re-render triggers ProtectedRoute redirect
    useAuthStore.setState({ token: null, customer: null })

    await waitFor(() => {
      expect(screen.getByText('Login Page')).toBeInTheDocument()
    })
  })
})
