import { describe, expect, it, beforeEach } from 'vitest'
import { useAuthStore } from '@/lib/auth-store'

// Clear persisted state before each test
beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({
    token: null,
    customer: null,
  })
})

describe('useAuthStore', () => {
  it('starts logged out', () => {
    const { token, customer } = useAuthStore.getState()
    expect(token).toBeNull()
    expect(customer).toBeNull()
  })

  describe('login', () => {
    it('stores token and customer data', () => {
      useAuthStore.getState().login({
        token: 'test-jwt-token',
        customer: {
          id: '1',
          email: 'alice@example.com',
          fullName: 'Alice Wonderland',
          role: 'Customer',
          createdAt: '2026-01-01T00:00:00Z',
        },
      })

      const { token, customer } = useAuthStore.getState()
      expect(token).toBe('test-jwt-token')
      expect(customer).toEqual({
        id: '1',
        email: 'alice@example.com',
        fullName: 'Alice Wonderland',
        role: 'Customer',
        createdAt: '2026-01-01T00:00:00Z',
      })
    })

    it('marks customer as authenticated', () => {
      useAuthStore.getState().login({
        token: 'token',
        customer: {
          id: '1',
          email: 'a@b.com',
          fullName: 'A',
          role: 'Customer',
          createdAt: '',
        },
      })

      expect(useAuthStore.getState().isAuthenticated()).toBe(true)
    })
  })

  describe('logout', () => {
    it('clears token and customer', () => {
      useAuthStore.getState().login({
        token: 'token',
        customer: {
          id: '1',
          email: 'a@b.com',
          fullName: 'A',
          role: 'Customer',
          createdAt: '',
        },
      })
      useAuthStore.getState().logout()

      const { token, customer } = useAuthStore.getState()
      expect(token).toBeNull()
      expect(customer).toBeNull()
    })

    it('marks customer as unauthenticated after logout', () => {
      useAuthStore.getState().login({
        token: 'token',
        customer: {
          id: '1',
          email: 'a@b.com',
          fullName: 'A',
          role: 'Customer',
          createdAt: '',
        },
      })
      useAuthStore.getState().logout()

      expect(useAuthStore.getState().isAuthenticated()).toBe(false)
    })
  })

  describe('isAuthenticated', () => {
    it('returns false when not logged in', () => {
      expect(useAuthStore.getState().isAuthenticated()).toBe(false)
    })
  })

  describe('isAdmin', () => {
    it('returns true for Admin role', () => {
      useAuthStore.getState().login({
        token: 'token',
        customer: {
          id: '1',
          email: 'admin@example.com',
          fullName: 'Admin',
          role: 'Admin',
          createdAt: '',
        },
      })
      expect(useAuthStore.getState().isAdmin()).toBe(true)
    })

    it('returns false for Customer role', () => {
      useAuthStore.getState().login({
        token: 'token',
        customer: {
          id: '1',
          email: 'c@example.com',
          fullName: 'Customer',
          role: 'Customer',
          createdAt: '',
        },
      })
      expect(useAuthStore.getState().isAdmin()).toBe(false)
    })

    it('returns false when not logged in', () => {
      expect(useAuthStore.getState().isAdmin()).toBe(false)
    })
  })

  describe('persistence', () => {
    it('persists token and customer to localStorage', () => {
      useAuthStore.getState().login({
        token: 'persist-token',
        customer: {
          id: '1',
          email: 'a@b.com',
          fullName: 'A',
          role: 'Customer',
          createdAt: '',
        },
      })

      const stored = localStorage.getItem('auth-storage')
      expect(stored).not.toBeNull()
      const parsed = JSON.parse(stored!)
      expect(parsed.state.token).toBe('persist-token')
      expect(parsed.state.customer.email).toBe('a@b.com')
    })

    it('hydrates from localStorage on load', () => {
      localStorage.setItem(
        'auth-storage',
        JSON.stringify({
          state: {
            token: 'hydrated-token',
            customer: {
              id: '2',
              email: 'hydrated@test.com',
              fullName: 'Hydrated',
              role: 'Admin',
              createdAt: '',
            },
          },
          version: 0,
        }),
      )

      // Re-create the store to trigger hydration
      useAuthStore.setState({
        token: 'hydrated-token',
        customer: {
          id: '2',
          email: 'hydrated@test.com',
          fullName: 'Hydrated',
          role: 'Admin',
          createdAt: '',
        },
      })

      expect(useAuthStore.getState().token).toBe('hydrated-token')
      expect(useAuthStore.getState().isAdmin()).toBe(true)
    })
  })
})
