import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { OrderConfirmation } from '@/pages/OrderConfirmation'
import { useAuthStore } from '@/lib/auth-store'

const mockOrderDto = {
  id: 42,
  status: 'Paid',
  subtotal: 2599.98,
  shippingFee: 5.99,
  total: 2605.97,
  shippingFullName: 'Jane Doe',
  shippingStreet: '123 Main St',
  shippingCity: 'Springfield',
  shippingPostalCode: '62701',
  shippingCountry: 'USA',
  shippingPhone: '+1-555-0100',
  createdAt: '2026-07-04T10:00:00Z',
  items: [
    {
      id: 1,
      productId: 1,
      productName: 'Laptop Pro',
      unitPrice: 1299.99,
      quantity: 2,
      subtotal: 2599.98,
    },
    {
      id: 2,
      productId: 5,
      productName: 'Wireless Mouse',
      unitPrice: 49.99,
      quantity: 1,
      subtotal: 49.99,
    },
  ],
}

function renderOrderConfirmation(orderId = 42) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/orders/${orderId}`]}>
        <Routes>
          <Route path="/orders/:id" element={<OrderConfirmation />} />
          <Route path="/products" element={<div>Products Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  useAuthStore.setState({
    token: 'test-token',
    customer: { id: '1', email: 'test@example.com', fullName: 'Test User', role: 'Customer', createdAt: '2026-01-01' },
  })
  server.resetHandlers()
  server.use(
    http.get(/\/api\/orders\/42$/, () =>
      HttpResponse.json({ success: true, data: mockOrderDto }),
    ),
  )
})

describe('OrderConfirmation page', () => {
  it('renders order confirmation heading', async () => {
    renderOrderConfirmation()
    expect(await screen.findByText(/order #42/i)).toBeInTheDocument()
  })

  it('shows the order status', async () => {
    renderOrderConfirmation()
    expect(await screen.findByText('Paid')).toBeInTheDocument()
  })

  it('displays the shipping address', async () => {
    renderOrderConfirmation()
    expect(await screen.findByText('Jane Doe')).toBeInTheDocument()
    expect(screen.getByText('123 Main St')).toBeInTheDocument()
    expect(screen.getByText('Springfield, 62701')).toBeInTheDocument()
    expect(screen.getByText('USA')).toBeInTheDocument()
  })

  it('lists all order items with names and quantities', async () => {
    renderOrderConfirmation()
    expect(await screen.findByText('Laptop Pro')).toBeInTheDocument()
    expect(screen.getByText('Wireless Mouse')).toBeInTheDocument()
    // Component shows "unitPrice × quantity" format
    expect(screen.getByText(/\$1,299\.99\s*×\s*2/)).toBeInTheDocument()
    expect(screen.getByText(/\$49\.99\s*×\s*1/)).toBeInTheDocument()
  })

  it('shows subtotal, shipping fee, and total', async () => {
    renderOrderConfirmation()
    await screen.findByText('Laptop Pro')
    // $2,599.98 appears both as item subtotal and order subtotal
    expect(screen.getAllByText(/\$2,599\.98/).length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText(/\$5\.99/)).toBeInTheDocument()
    expect(screen.getByText(/\$2,605\.97/)).toBeInTheDocument()
  })

  it('shows a link to continue shopping', async () => {
    renderOrderConfirmation()
    await screen.findByText('Laptop Pro')
    expect(screen.getByRole('link', { name: /continue shopping/i })).toHaveAttribute('href', '/products')
  })

  it('shows a link to view my orders', async () => {
    renderOrderConfirmation()
    await screen.findByText('Laptop Pro')
    expect(screen.getByRole('link', { name: /view my orders/i })).toHaveAttribute('href', '/orders')
  })

  it('shows loading state while fetching order', () => {
    renderOrderConfirmation()
    expect(screen.getByText(/loading/i)).toBeInTheDocument()
  })

  it('shows error for non-existent order', async () => {
    server.use(
      http.get(/\/api\/orders\/999$/, () =>
        HttpResponse.json(
          { success: false, error: { code: 'ORDER_NOT_FOUND', message: 'Order not found.' } },
          { status: 404 },
        ),
      ),
    )

    renderOrderConfirmation(999)

    await waitFor(() => {
      expect(screen.getByText(/order not found/i)).toBeInTheDocument()
    })
  })
})
