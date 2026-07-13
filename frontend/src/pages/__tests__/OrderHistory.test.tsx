import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { OrderHistory } from '@/pages/OrderHistory'
import { useAuthStore } from '@/lib/auth-store'

const mockOrders = [
  {
    id: 1,
    status: 'Paid',
    subtotal: 1299.99,
    shippingFee: 5.99,
    total: 1305.98,
    shippingFullName: 'Jane Doe',
    shippingStreet: '123 Main St',
    shippingCity: 'Springfield',
    shippingPostalCode: '62701',
    shippingCountry: 'USA',
    shippingPhone: '+1-555-0100',
    createdAt: '2026-07-04T10:00:00Z',
    items: [
      { id: 1, productId: 1, productName: 'Laptop Pro', unitPrice: 1299.99, quantity: 1, subtotal: 1299.99 },
    ],
  },
  {
    id: 2,
    status: 'Pending',
    subtotal: 49.99,
    shippingFee: 5.99,
    total: 55.98,
    shippingFullName: 'John Smith',
    shippingStreet: '456 Oak Ave',
    shippingCity: 'Chicago',
    shippingPostalCode: '60601',
    shippingCountry: 'USA',
    shippingPhone: '+1-555-0200',
    createdAt: '2026-07-03T15:00:00Z',
    items: [
      { id: 2, productId: 5, productName: 'Wireless Mouse', unitPrice: 49.99, quantity: 1, subtotal: 49.99 },
    ],
  },
]

function renderOrderHistory() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/orders']}>
        <OrderHistory />
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
  // Default: return populated orders list
  server.use(
    http.get(/\/api\/orders$/, () =>
      HttpResponse.json({
        success: true,
        data: mockOrders,
        meta: { page: 1, pageSize: 10, totalCount: 2, totalPages: 1 },
      }),
    ),
  )
})

describe('OrderHistory page', () => {
  it('renders the page heading', async () => {
    renderOrderHistory()
    expect(await screen.findByText(/my orders/i)).toBeInTheDocument()
  })

  it('displays a list of orders', async () => {
    renderOrderHistory()
    expect(await screen.findByText(/order #1/i)).toBeInTheDocument()
    expect(screen.getByText(/order #2/i)).toBeInTheDocument()
  })

  it('shows order status badges', async () => {
    renderOrderHistory()
    await screen.findByText(/order #1/i)
    expect(screen.getByText('Paid')).toBeInTheDocument()
    expect(screen.getByText('Pending')).toBeInTheDocument()
  })

  it('shows order totals', async () => {
    renderOrderHistory()
    await screen.findByText(/order #1/i)
    expect(screen.getByText(/\$1,305\.98/)).toBeInTheDocument()
    expect(screen.getByText(/\$55\.98/)).toBeInTheDocument()
  })

  it('links each order to its detail page', async () => {
    renderOrderHistory()
    await screen.findByText(/order #1/i)
    const links = screen.getAllByRole('link', { name: /view details/i })
    expect(links).toHaveLength(2)
    expect(links[0]).toHaveAttribute('href', '/orders/1')
    expect(links[1]).toHaveAttribute('href', '/orders/2')
  })

  it('shows empty state when no orders exist', async () => {
    server.use(
      http.get(/\/api\/orders$/, () =>
        HttpResponse.json({
          success: true,
          data: [],
          meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
        }),
      ),
    )

    renderOrderHistory()

    await waitFor(() => {
      expect(screen.getByText(/no orders yet/i)).toBeInTheDocument()
    })
  })

  it('shows a link to browse products when empty', async () => {
    server.use(
      http.get(/\/api\/orders$/, () =>
        HttpResponse.json({
          success: true,
          data: [],
          meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
        }),
      ),
    )

    renderOrderHistory()

    await waitFor(() => {
      expect(screen.getByText(/no orders yet/i)).toBeInTheDocument()
    })
    expect(screen.getByRole('link', { name: /browse products/i })).toHaveAttribute('href', '/products')
  })

  it('renders pagination when more than one page exists', async () => {
    server.use(
      http.get(/\/api\/orders$/, () =>
        HttpResponse.json({
          success: true,
          data: mockOrders,
          meta: { page: 1, pageSize: 10, totalCount: 25, totalPages: 3 },
        }),
      ),
    )

    renderOrderHistory()

    await waitFor(() => {
      expect(screen.getByRole('navigation', { name: /pagination/i })).toBeInTheDocument()
    })
  })

  it('fetches the next page when next button is clicked', async () => {
    let lastRequestedPage: number | null = null
    server.use(
      http.get(/\/api\/orders$/, ({ request }) => {
        const url = new URL(request.url)
        lastRequestedPage = Number(url.searchParams.get('page') ?? 1)
        return HttpResponse.json({
          success: true,
          data: mockOrders,
          meta: { page: lastRequestedPage, pageSize: 10, totalCount: 25, totalPages: 3 },
        })
      }),
    )

    renderOrderHistory()
    await screen.findByText(/order #1/i)

    fireEvent.click(screen.getByRole('button', { name: /next/i }))

    await waitFor(() => {
      expect(lastRequestedPage).toBe(2)
    })
  })
})
