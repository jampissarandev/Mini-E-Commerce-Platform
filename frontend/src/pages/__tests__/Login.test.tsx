import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { Login } from '@/pages/Login'
import { useAuthStore } from '@/lib/auth-store'

function renderLogin() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/login']}>
        <Login />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, customer: null })
})

describe('Login page', () => {
  it('renders email and password fields', () => {
    renderLogin()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument()
  })

  it('renders login button', () => {
    renderLogin()
    expect(screen.getByRole('button', { name: /log in/i })).toBeInTheDocument()
  })

  it('shows link to register page', () => {
    renderLogin()
    expect(screen.getByRole('link', { name: /register/i })).toBeInTheDocument()
  })

  it('validates email is required', async () => {
    renderLogin()

    fireEvent.submit(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      expect(screen.getByText(/email is required/i)).toBeInTheDocument()
    })
  })

  it('validates password is required', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    fireEvent.submit(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      expect(screen.getByText(/password is required/i)).toBeInTheDocument()
    })
  })

  it('validates email format', async () => {
    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'not-an-email')
    await user.type(screen.getByLabelText(/password/i), 'password123')
    fireEvent.submit(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      expect(screen.getByText(/invalid email/i)).toBeInTheDocument()
    })
  })

  it('submits and stores auth data on success', async () => {
    server.use(
      http.post('**/api/auth/login', () =>
        HttpResponse.json({
          success: true,
          data: {
            token: 'jwt-token-123',
            expiresAt: '2026-12-31T23:59:59Z',
            customer: {
              id: '1',
              email: 'alice@example.com',
              fullName: 'Alice Wonderland',
              role: 'Customer',
              createdAt: '2026-01-01T00:00:00Z',
            },
          },
        }),
      ),
    )

    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/password/i), 'Password123')
    await user.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      const state = useAuthStore.getState()
      expect(state.token).toBe('jwt-token-123')
      expect(state.customer?.email).toBe('alice@example.com')
      expect(state.customer?.role).toBe('Customer')
    })
  })

  it('displays error message on invalid credentials', async () => {
    server.use(
      http.post('**/api/auth/login', () =>
        HttpResponse.json(
          {
            success: false,
            error: {
              code: 'INVALID_CREDENTIALS',
              message: 'Invalid email or password.',
            },
          },
          { status: 401 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'wrong@example.com')
    await user.type(screen.getByLabelText(/password/i), 'wrongpass')
    await user.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      expect(screen.getByText(/invalid email or password/i)).toBeInTheDocument()
    })
  })

  it('shows loading state while submitting', async () => {
    server.use(
      http.post('**/api/auth/login', async () => {
        // Simulate slow response
        await new Promise((resolve) => setTimeout(resolve, 100))
        return HttpResponse.json({
          success: true,
          data: {
            token: 'jwt-token',
            expiresAt: '2026-12-31T23:59:59Z',
            customer: {
              id: '1',
              email: 'a@b.com',
              fullName: 'A',
              role: 'Customer',
              createdAt: '',
            },
          },
        })
      }),
    )

    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/password/i), 'Password123')
    await user.click(screen.getByRole('button', { name: /log in/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /logging in/i })).toBeDisabled()
    })
  })
})
