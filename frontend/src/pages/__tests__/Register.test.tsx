import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { Register } from '@/pages/Register'
import { useAuthStore } from '@/lib/auth-store'

function renderRegister() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/register']}>
        <Register />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, user: null })
})

describe('Register page', () => {
  it('renders name, email, and password fields', () => {
    renderRegister()
    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument()
  })

  it('renders register button', () => {
    renderRegister()
    expect(screen.getByRole('button', { name: /create account/i })).toBeInTheDocument()
  })

  it('shows link to login page', () => {
    renderRegister()
    expect(screen.getByRole('link', { name: /log in/i })).toBeInTheDocument()
  })

  it('validates full name is required', async () => {
    renderRegister()

    fireEvent.submit(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => {
      expect(screen.getByText(/full name is required/i)).toBeInTheDocument()
    })
  })

  it('validates email is required', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    fireEvent.submit(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => {
      expect(screen.getByText(/email is required/i)).toBeInTheDocument()
    })
  })

  it('validates password minimum length', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/password/i), '12345')
    fireEvent.submit(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => {
      expect(screen.getByText(/at least 6 characters/i)).toBeInTheDocument()
    })
  })

  it('submits and stores auth data on success', async () => {
    server.use(
      http.post('**/api/auth/register', () =>
        HttpResponse.json(
          {
            success: true,
            data: {
              token: 'new-jwt-token',
              expiresAt: '2026-12-31T23:59:59Z',
              user: {
                id: '2',
                email: 'alice@example.com',
                fullName: 'Alice Wonderland',
                role: 'Customer',
                createdAt: '2026-01-01T00:00:00Z',
              },
            },
          },
          { status: 201 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/full name/i), 'Alice Wonderland')
    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/password/i), 'Password123')
    fireEvent.submit(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => {
      const state = useAuthStore.getState()
      expect(state.token).toBe('new-jwt-token')
      expect(state.user?.email).toBe('alice@example.com')
    })
  })

  it('displays error message on registration failure', async () => {
    server.use(
      http.post('**/api/auth/register', () =>
        HttpResponse.json(
          {
            success: false,
            error: {
              code: 'REGISTRATION_FAILED',
              message: 'Could not create user account.',
              details: {
                DuplicateUserName: ['Username already taken.'],
              },
            },
          },
          { status: 400 },
        ),
      ),
    )

    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/full name/i), 'Alice')
    await user.type(screen.getByLabelText(/email/i), 'alice@example.com')
    await user.type(screen.getByLabelText(/password/i), 'Password123')
    fireEvent.submit(screen.getByRole('button', { name: /create account/i }))

    await waitFor(() => {
      expect(screen.getByText(/could not create user account/i)).toBeInTheDocument()
    })
  })
})
