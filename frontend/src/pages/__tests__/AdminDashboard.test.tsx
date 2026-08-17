import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { AdminDashboard } from '@/pages/admin/Dashboard'

// ── Test data ──────────────────────────────────────────────

const summary = {
  success: true,
  data: {
    totalOrders: 42,
    totalRevenue: 1234.56,
    totalCustomers: 17,
    totalProducts: 20,
    lowStockCount: 3,
  },
}

const salesData = {
  success: true,
  data: [
    { date: '2026-07-01', total: 100, orderCount: 2 },
    { date: '2026-07-02', total: 150, orderCount: 3 },
    { date: '2026-07-03', total: 0, orderCount: 0 },
  ],
}

const recentOrders = {
  success: true,
  data: [
    {
      id: 1,
      customerId: 'cust-1',
      customerEmail: 'alice@example.com',
      status: 'Pending',
      total: 110,
      itemCount: 2,
      createdAt: '2026-07-10T10:00:00Z',
    },
    {
      id: 2,
      customerId: 'cust-2',
      customerEmail: 'bob@example.com',
      status: 'Paid',
      total: 49.99,
      itemCount: 1,
      createdAt: '2026-07-11T12:00:00Z',
    },
  ],
}

const lowStock = {
  success: true,
  data: [
    { id: 1, name: 'Wireless Headphones', stock: 5 },
    { id: 2, name: 'Denim Jacket', stock: 0 },
  ],
}

// ── Helpers ────────────────────────────────────────────────

function renderDashboard() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin']}>
        <AdminDashboard />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

// ── Tests ──────────────────────────────────────────────────

describe('AdminDashboard page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders the page heading', async () => {
    renderDashboard()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument()
    })
  })

  it('displays 4 KPI cards with values from the summary endpoint', async () => {
    server.use(
      http.get(/\/api\/admin\/dashboard\/summary$/, () =>
        HttpResponse.json(summary),
      ),
    )

    renderDashboard()

    await waitFor(() => {
      expect(screen.getByText('Total Orders')).toBeInTheDocument()
    })

    expect(screen.getByText('42')).toBeInTheDocument()
    expect(screen.getByText('$1,234.56')).toBeInTheDocument()
    expect(screen.getByText('17')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('Revenue')).toBeInTheDocument()
    expect(screen.getByText('Customers')).toBeInTheDocument()
    // "Low Stock" appears both as a KPI card title and as the low-stock
    // section header — assert at least one is present.
    expect(screen.getAllByText('Low Stock').length).toBeGreaterThan(0)
  })

  it('shows a loading skeleton while the summary is in flight', async () => {
    server.use(
      http.get(/\/api\/admin\/dashboard\/summary$/, () =>
        new Promise(() => {}),
      ),
    )

    renderDashboard()

    expect(
      screen.getByRole('status', { name: /loading dashboard summary/i }),
    ).toBeInTheDocument()
  })

  it('renders the sales chart when there is data', async () => {
    server.use(
      http.get(/\/api\/admin\/dashboard\/sales$/, () =>
        HttpResponse.json(salesData),
      ),
    )

    renderDashboard()

    await waitFor(() => {
      expect(screen.getByRole('img', { name: 'Sales chart' })).toBeInTheDocument()
    })
  })

  it('renders recent orders with status badges linking to detail', async () => {
    server.use(
      http.get(/\/api\/admin\/dashboard\/recent-orders$/, () =>
        HttpResponse.json(recentOrders),
      ),
    )

    renderDashboard()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    expect(screen.getByText('bob@example.com')).toBeInTheDocument()
    expect(screen.getByText('Pending')).toBeInTheDocument()
    expect(screen.getByText('Paid')).toBeInTheDocument()
    expect(screen.getByText('$110.00')).toBeInTheDocument()

    const viewLink = screen.getByRole('link', { name: /view order 1/i })
    expect(viewLink).toHaveAttribute('href', '/admin/orders/1')
  })

  it('renders the low stock table with edit links', async () => {
    server.use(
      http.get(/\/api\/admin\/dashboard\/summary$/, () =>
        HttpResponse.json(summary),
      ),
      http.get(/\/api\/admin\/dashboard\/low-stock$/, () =>
        HttpResponse.json(lowStock),
      ),
    )

    renderDashboard()

    await waitFor(() => {
      expect(screen.getByText('Wireless Headphones')).toBeInTheDocument()
    })

    expect(screen.getByText('Denim Jacket')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()

    const editLink = screen.getByRole('link', { name: /edit wireless headphones/i })
    expect(editLink).toHaveAttribute('href', '/admin/products')
  })

  it('shows empty states when there is no data', async () => {
    renderDashboard()

    await waitFor(() => {
      expect(screen.getByText('No sales data yet')).toBeInTheDocument()
    })

    expect(screen.getByText('No orders yet')).toBeInTheDocument()
    expect(screen.getByText('All products are well stocked')).toBeInTheDocument()
  })
})