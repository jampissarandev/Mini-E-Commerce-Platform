import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { AdminOrders } from '@/pages/admin/Orders'

// ── Test data ──────────────────────────────────────────────

const adminOrdersPage = {
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
  meta: { page: 1, pageSize: 10, totalCount: 2, totalPages: 1 },
}

const emptyOrders = {
  success: true,
  data: [],
  meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
}

// ── Helpers ────────────────────────────────────────────────

function renderAdminOrders(route = '/admin/orders') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <AdminOrders />
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}

// ── Tests ──────────────────────────────────────────────────

describe('AdminOrders page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders the page heading', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, () => HttpResponse.json(emptyOrders)),
    )

    renderAdminOrders()

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /orders management/i }),
      ).toBeInTheDocument()
    })
  })

  it('displays orders in a table with id, email, total, status, and created date', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, () => HttpResponse.json(adminOrdersPage)),
    )

    renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    expect(screen.getByText('bob@example.com')).toBeInTheDocument()
    expect(screen.getByText('$110.00')).toBeInTheDocument()
    expect(screen.getByText('$49.99')).toBeInTheDocument()
    expect(screen.getByText('Pending')).toBeInTheDocument()
    expect(screen.getByText('Paid')).toBeInTheDocument()
  })

  it('shows an empty state when there are no orders', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, () => HttpResponse.json(emptyOrders)),
    )

    renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('No orders found')).toBeInTheDocument()
    })
  })

  it('filters by status and updates the results', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, ({ request }) => {
        const url = new URL(request.url)
        const status = url.searchParams.get('status')
        if (status === 'Paid') {
          return HttpResponse.json({
            ...adminOrdersPage,
            data: [adminOrdersPage.data[1]],
          })
        }
        return HttpResponse.json(adminOrdersPage)
      }),
    )

    const { user } = renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('combobox', { name: /filter by status/i }))
    await user.click(await screen.findByRole('option', { name: 'Paid' }))

    await waitFor(() => {
      expect(screen.getByText('bob@example.com')).toBeInTheDocument()
      expect(screen.queryByText('alice@example.com')).not.toBeInTheDocument()
    })
  })

  it('searches by free text and updates the results', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, ({ request }) => {
        const url = new URL(request.url)
        const q = url.searchParams.get('q')
        if (q === 'bob') {
          return HttpResponse.json({
            ...adminOrdersPage,
            data: [adminOrdersPage.data[1]],
          })
        }
        return HttpResponse.json(adminOrdersPage)
      }),
    )

    const { user } = renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    await user.type(screen.getByLabelText(/search orders/i), 'bob')

    await waitFor(() => {
      expect(screen.getByText('bob@example.com')).toBeInTheDocument()
      expect(screen.queryByText('alice@example.com')).not.toBeInTheDocument()
    })
  })

  it('sends from/to date range params to the API', async () => {
    let lastUrl: URL | null = null
    server.use(
      http.get(/\/api\/admin\/orders$/, ({ request }) => {
        lastUrl = new URL(request.url)
        return HttpResponse.json(adminOrdersPage)
      }),
    )

    const { user } = renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    await user.type(screen.getByLabelText(/from date/i), '2026-07-01')
    await user.type(screen.getByLabelText(/to date/i), '2026-07-13')

    await waitFor(() => {
      expect(lastUrl?.searchParams.get('from')).toBe('2026-07-01')
      expect(lastUrl?.searchParams.get('to')).toBe('2026-07-13')
    })
  })

  it('links each order row to its detail page', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, () => HttpResponse.json(adminOrdersPage)),
    )

    renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument()
    })

    const viewLink = screen.getByRole('link', { name: /view order 1/i })
    expect(viewLink).toHaveAttribute('href', '/admin/orders/1')
  })

  it('renders pagination when there is more than one page', async () => {
    server.use(
      http.get(/\/api\/admin\/orders$/, () =>
        HttpResponse.json({
          ...adminOrdersPage,
          meta: { page: 1, pageSize: 10, totalCount: 25, totalPages: 3 },
        }),
      ),
    )

    renderAdminOrders()

    await waitFor(() => {
      expect(screen.getByRole('navigation', { name: /pagination/i })).toBeInTheDocument()
    })
  })
})