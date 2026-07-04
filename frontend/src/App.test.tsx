// Smoke test: confirms the React app boots, the router resolves routes, and
// the page-level components render without throwing. This is the canary for
// the entire test harness (MSW + jsdom + RTL).
//
// We wrap the app in MemoryRouter (not BrowserRouter) so the test does not
// need a real URL or window.history, and in a fresh QueryClient because
// the production one is created at module scope in main.tsx.

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, beforeEach } from 'vitest'
import App from './App'
import { useAuthStore } from '@/lib/auth-store'

function renderAt(path: string) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  useAuthStore.setState({ token: null, user: null })
})

describe('App', () => {
  it('renders the home page at "/"', () => {
    renderAt('/')
    expect(screen.getByRole('heading', { name: /welcome to mini e-commerce/i }))
      .toBeInTheDocument()
  })

  it('renders the 404 page for an unknown route', () => {
    renderAt('/this-route-does-not-exist')
    expect(screen.getByRole('heading', { name: '404' })).toBeInTheDocument()
    expect(screen.getByText(/page not found/i)).toBeInTheDocument()
  })

  it('redirects unauthenticated users away from /checkout to /login', () => {
    renderAt('/checkout')
    expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
  })

  it('redirects unauthenticated users away from /orders to /login', () => {
    renderAt('/orders')
    expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
  })

  it('redirects unauthenticated users away from /orders/:id to /login', () => {
    renderAt('/orders/1')
    expect(screen.getByText(/welcome back/i)).toBeInTheDocument()
  })
})
