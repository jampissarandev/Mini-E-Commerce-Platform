import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, beforeEach } from 'vitest'
import { Navbar } from '@/components/Navbar'
import { useAuthStore } from '@/lib/auth-store'
import { server } from '@/test/server'

function renderNavbar() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  server.resetHandlers()
})

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, user: null })
})

describe('Navbar', () => {
  it('renders logo/brand', () => {
    renderNavbar()
    expect(screen.getByText(/mini e-commerce/i)).toBeInTheDocument()
  })

  it('shows Products link', () => {
    renderNavbar()
    expect(screen.getByRole('link', { name: /products/i })).toBeInTheDocument()
  })

  describe('when logged out', () => {
    it('shows Login button', () => {
      renderNavbar()
      expect(screen.getByRole('link', { name: /log in/i })).toBeInTheDocument()
    })

    it('shows Register button', () => {
      renderNavbar()
      expect(screen.getByRole('link', { name: /register/i })).toBeInTheDocument()
    })

    it('does not show user name', () => {
      renderNavbar()
      expect(screen.queryByText(/welcome/i)).not.toBeInTheDocument()
    })

    it('does not show logout button', () => {
      renderNavbar()
      expect(screen.queryByRole('button', { name: /log out/i })).not.toBeInTheDocument()
    })
  })

  describe('when logged in as Customer', () => {
    beforeEach(() => {
      useAuthStore.getState().login({
        token: 'token',
        user: {
          id: '1',
          email: 'alice@example.com',
          fullName: 'Alice Wonderland',
          role: 'Customer',
          createdAt: '',
        },
      })
    })

    it('shows user greeting', () => {
      renderNavbar()
      expect(screen.getByText(/alice/i)).toBeInTheDocument()
    })

    it('shows logout button', () => {
      renderNavbar()
      expect(screen.getByRole('button', { name: /log out/i })).toBeInTheDocument()
    })

    it('does not show Login button', () => {
      renderNavbar()
      expect(screen.queryByRole('link', { name: /log in$/i })).not.toBeInTheDocument()
    })

    it('does not show Register button', () => {
      renderNavbar()
      expect(screen.queryByRole('link', { name: /register/i })).not.toBeInTheDocument()
    })

    it('shows Orders link', () => {
      renderNavbar()
      expect(screen.getByRole('link', { name: /orders/i })).toHaveAttribute('href', '/orders')
    })

    it('does not show Admin link', () => {
      renderNavbar()
      expect(screen.queryByRole('link', { name: /admin/i })).not.toBeInTheDocument()
    })

    it('logs out when logout button is clicked', async () => {
      const user = userEvent.setup()
      renderNavbar()

      await user.click(screen.getByRole('button', { name: /log out/i }))

      expect(useAuthStore.getState().token).toBeNull()
      expect(useAuthStore.getState().user).toBeNull()
    })
  })

  describe('when logged in as Admin', () => {
    beforeEach(() => {
      useAuthStore.getState().login({
        token: 'admin-token',
        user: {
          id: '1',
          email: 'admin@example.com',
          fullName: 'Admin User',
          role: 'Admin',
          createdAt: '',
        },
      })
    })

    it('shows Admin link', () => {
      renderNavbar()
      expect(screen.getByRole('link', { name: /admin/i })).toBeInTheDocument()
    })

    it('shows Orders link', () => {
      renderNavbar()
      expect(screen.getByRole('link', { name: /orders/i })).toBeInTheDocument()
    })

    it('shows user greeting', () => {
      renderNavbar()
      expect(screen.getByText(/admin user/i)).toBeInTheDocument()
    })
  })
})
