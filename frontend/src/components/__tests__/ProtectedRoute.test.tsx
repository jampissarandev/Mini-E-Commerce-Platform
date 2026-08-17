import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, beforeEach } from 'vitest'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { useAuthStore } from '@/lib/auth-store'

function renderWithRouter(
  path: string,
  options?: { requiredRole?: string },
) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route
            path="/protected"
            element={
              <ProtectedRoute requiredRole={options?.requiredRole}>
                <div>Protected Content</div>
              </ProtectedRoute>
            }
          />
          <Route path="/login" element={<div>Login Page</div>} />
          <Route path="/admin" element={<div>Admin Dashboard</div>} />
          <Route path="*" element={<div>Redirected Home</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, customer: null })
})

describe('ProtectedRoute', () => {
  it('redirects to /login when not authenticated', () => {
    renderWithRouter('/protected')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('renders children when authenticated', () => {
    useAuthStore.getState().login({
      token: 'test-token',
      customer: {
        id: '1',
        email: 'a@b.com',
        fullName: 'A',
        role: 'Customer',
        createdAt: '',
      },
    })

    renderWithRouter('/protected')
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })

  it('redirects to /login when authenticated but wrong role', () => {
    useAuthStore.getState().login({
      token: 'test-token',
      customer: {
        id: '1',
        email: 'a@b.com',
        fullName: 'A',
        role: 'Customer',
        createdAt: '',
      },
    })

    renderWithRouter('/protected', { requiredRole: 'Admin' })
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('renders children when authenticated with correct role', () => {
    useAuthStore.getState().login({
      token: 'test-token',
      customer: {
        id: '1',
        email: 'admin@b.com',
        fullName: 'Admin',
        role: 'Admin',
        createdAt: '',
      },
    })

    renderWithRouter('/protected', { requiredRole: 'Admin' })
    expect(screen.getByText('Protected Content')).toBeInTheDocument()
  })
})
