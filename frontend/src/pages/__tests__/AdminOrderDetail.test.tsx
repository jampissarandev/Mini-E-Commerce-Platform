import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { AdminOrderDetail } from '@/pages/admin/OrderDetail'

// ── Test data ──────────────────────────────────────────────

const pendingOrder = {
  success: true,
  data: {
    id: 1,
    status: 'Pending',
    subtotal: 100,
    shippingFee: 10,
    total: 110,
    shippingFullName: 'Jane Doe',
    shippingStreet: '123 Main St',
    shippingCity: 'Springfield',
    shippingPostalCode: '12345',
    shippingCountry: 'USA',
    shippingPhone: '555-1234',
    createdAt: '2026-07-10T10:00:00Z',
    customer: {
      id: 'cust-1',
      email: 'jane@example.com',
      fullName: 'Jane Doe',
    },
    items: [
      {
        id: 1,
        productId: 1,
        productName: 'Laptop',
        unitPrice: 100,
        quantity: 1,
        imageUrl: '/images/laptop.jpg',
        subtotal: 100,
      },
    ],
    allowedNextStatuses: ['Paid', 'Cancelled'],
  },
}

// ── Helpers ────────────────────────────────────────────────

function renderDetail(route = '/admin/orders/1') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <Routes>
            <Route path="/admin/orders/:id" element={<AdminOrderDetail />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}

// ── Tests ──────────────────────────────────────────────────

describe('AdminOrderDetail page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders customer, items, shipping, and totals', async () => {
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () => HttpResponse.json(pendingOrder)),
    )

    renderDetail()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /order #1/i })).toBeInTheDocument()
    })

    expect(screen.getByText('jane@example.com')).toBeInTheDocument()
    expect(screen.getByText('Laptop')).toBeInTheDocument()
    expect(screen.getByText('123 Main St')).toBeInTheDocument()
    expect(screen.getByText('Springfield 12345')).toBeInTheDocument()
    expect(screen.getByText('$110.00')).toBeInTheDocument()
  })

  it('shows the mock payment badge as "Not captured (mock)" for a Pending order', async () => {
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () => HttpResponse.json(pendingOrder)),
    )

    renderDetail()

    await waitFor(() => {
      expect(screen.getByText('Not captured (mock)')).toBeInTheDocument()
    })
  })

  it('shows the mock payment badge as "Captured (mock)" for a Paid order', async () => {
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () =>
        HttpResponse.json({
          ...pendingOrder,
          data: { ...pendingOrder.data, status: 'Paid' },
        }),
      ),
    )

    renderDetail()

    await waitFor(() => {
      expect(screen.getByText('Captured (mock)')).toBeInTheDocument()
    })
  })

  it('status dropdown offers only the valid transitions from the server', async () => {
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () => HttpResponse.json(pendingOrder)),
    )

    const { user } = renderDetail()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /order #1/i })).toBeInTheDocument()
    })

    await user.click(screen.getByRole('combobox', { name: /update order status/i }))

    expect(await screen.findByRole('option', { name: 'Paid' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Cancelled' })).toBeInTheDocument()
    // Invalid transitions are not offered.
    expect(screen.queryByRole('option', { name: 'Shipped' })).not.toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'Delivered' })).not.toBeInTheDocument()
  })

  it('updating the status persists and reflects the new status', async () => {
    let currentOrder = { ...pendingOrder }
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () => HttpResponse.json(currentOrder)),
      http.put(/\/api\/admin\/orders\/1\/status$/, () => {
        currentOrder = {
          ...currentOrder,
          data: {
            ...currentOrder.data,
            status: 'Paid',
            allowedNextStatuses: ['Shipped', 'Cancelled'],
          },
        }
        return HttpResponse.json(currentOrder)
      }),
    )

    const { user } = renderDetail()

    await waitFor(() => {
      expect(screen.getByText('Pending')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('combobox', { name: /update order status/i }))
    await user.click(await screen.findByRole('option', { name: 'Paid' }))

    await waitFor(() => {
      expect(screen.getByText('Paid')).toBeInTheDocument()
      expect(screen.getByText('Captured (mock)')).toBeInTheDocument()
    })
  })

  it('shows a not-found state when the order does not exist', async () => {
    server.use(
      http.get(/\/api\/admin\/orders\/\d+$/, () =>
        HttpResponse.json(
          { success: false, error: { code: 'ORDER_NOT_FOUND', message: 'not found' } },
          { status: 404 },
        ),
      ),
    )

    renderDetail()

    await waitFor(() => {
      expect(screen.getByText('Order not found')).toBeInTheDocument()
    })
  })
})