import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { Checkout } from '@/pages/Checkout'
import { useAuthStore } from '@/lib/auth-store'

const mockCartDto = {
  success: true,
  data: {
    id: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [
      {
        id: 1,
        productId: 1,
        productName: 'Laptop Pro',
        productSlug: 'laptop-pro',
        imageUrl: '/images/laptop.jpg',
        unitPrice: 1299.99,
        quantity: 2,
        subtotal: 2599.98,
      },
    ],
    total: 2605.97,
  },
}

const mockOrderDto = {
  id: 1,
  status: 'Paid',
  subtotal: 2599.98,
  shippingFee: 5.99,
  total: 2605.97,
  shippingAddress: '123 Main St, Springfield, IL 62701',
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
  ],
}

function renderCheckout() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/checkout']}>
        <Routes>
          <Route path="/checkout" element={<Checkout />} />
          <Route path="/orders/:id" element={<div>Order Confirmation</div>} />
          <Route path="/cart" element={<div>Cart Page</div>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function fillShippingForm() {
  fireEvent.change(screen.getByLabelText(/^street/i), {
    target: { value: '123 Main St' },
  })
  fireEvent.change(screen.getByLabelText(/^city/i), {
    target: { value: 'Springfield' },
  })
  fireEvent.change(screen.getByLabelText(/postal code/i), {
    target: { value: '62701' },
  })
  fireEvent.change(screen.getByLabelText(/^country/i), {
    target: { value: 'USA' },
  })
  fireEvent.change(screen.getByLabelText(/^phone/i), {
    target: { value: '+1-555-0100' },
  })
}

beforeEach(() => {
  useAuthStore.setState({
    token: 'test-token',
    customer: { id: '1', email: 'test@example.com', fullName: 'Test User', role: 'Customer', createdAt: '2026-01-01' },
  })
  server.resetHandlers()
  // Set up non-empty cart for all tests by default
  server.use(
    http.get(/\/api\/cart$/, () =>
      HttpResponse.json(mockCartDto),
    ),
  )
})

describe('Checkout page', () => {
  it('renders all six shipping form fields', async () => {
    renderCheckout()
    expect(await screen.findByLabelText(/full name/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^street/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^city/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/postal code/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^country/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^phone/i)).toBeInTheDocument()
  })

  it('pre-fills full name from the authenticated user', async () => {
    renderCheckout()
    const fullNameInput = await screen.findByLabelText(/full name/i) as HTMLInputElement
    expect(fullNameInput.value).toBe('Test User')
  })

  it('renders an order summary with cart items', async () => {
    renderCheckout()
    expect(await screen.findByText('Laptop Pro')).toBeInTheDocument()
    // The order summary shows the subtotal per item line
    expect(screen.getByText(/\$2,599\.98/)).toBeInTheDocument()
  })

  it('shows the cart total including shipping', async () => {
    renderCheckout()
    await screen.findByText('Laptop Pro')
    expect(screen.getByText(/\$2,605\.97/)).toBeInTheDocument()
  })

  it('validates all required fields when form is empty', async () => {
    // Logout so fullName is not pre-filled.
    useAuthStore.setState({ token: null, customer: null })

    renderCheckout()
    await screen.findByText('Laptop Pro')

    fireEvent.submit(screen.getByRole('button', { name: /place order/i }))

    await waitFor(() => {
      expect(screen.getByText(/full name is required/i)).toBeInTheDocument()
      expect(screen.getByText(/street is required/i)).toBeInTheDocument()
      expect(screen.getByText(/city is required/i)).toBeInTheDocument()
      expect(screen.getByText(/postal code is required/i)).toBeInTheDocument()
      expect(screen.getByText(/country is required/i)).toBeInTheDocument()
      expect(screen.getByText(/phone is required/i)).toBeInTheDocument()
    })
  })

  it('redirects to order confirmation on successful checkout', async () => {
    server.use(
      http.post(/\/api\/orders$/, () =>
        HttpResponse.json({ success: true, data: mockOrderDto }, { status: 201 }),
      ),
    )

    renderCheckout()
    await screen.findByText('Laptop Pro')

    fillShippingForm()
    fireEvent.submit(screen.getByRole('button', { name: /place order/i }))

    await waitFor(() => {
      expect(screen.getByText('Order Confirmation')).toBeInTheDocument()
    })
  })

  it('shows error message on payment failure', async () => {
    server.use(
      http.post(/\/api\/orders$/, () =>
        HttpResponse.json(
          { success: false, error: { code: 'PAYMENT_FAILED', message: 'Payment processing failed.' } },
          { status: 400 },
        ),
      ),
    )

    renderCheckout()
    await screen.findByText('Laptop Pro')

    fillShippingForm()
    fireEvent.submit(screen.getByRole('button', { name: /place order/i }))

    await waitFor(() => {
      expect(screen.getByText(/payment processing failed/i)).toBeInTheDocument()
    })
  })

  it('redirects to cart when cart is empty', async () => {
    server.use(
      http.get(/\/api\/cart$/, () =>
        HttpResponse.json({
          success: true,
          data: {
            id: 1,
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
            items: [],
            total: 0,
          },
        }),
      ),
    )

    renderCheckout()

    await waitFor(() => {
      expect(screen.getByText('Cart Page')).toBeInTheDocument()
    })
  })

  it('hides the payment-mode banner when mode is AlwaysSucceed', async () => {
    server.use(
      http.get(/\/api\/payments\/mock-mode$/, () =>
        HttpResponse.json({
          success: true,
          data: { mode: 'AlwaysSucceed', failIfAmountGreaterThan: null },
        }),
      ),
    )
    renderCheckout()
    await screen.findByText('Laptop Pro')
    expect(screen.queryByTestId('payment-mode-banner')).not.toBeInTheDocument()
  })

  it('shows the payment-mode banner when mode is AlwaysFail', async () => {
    server.use(
      http.get(/\/api\/payments\/mock-mode$/, () =>
        HttpResponse.json({
          success: true,
          data: { mode: 'AlwaysFail', failIfAmountGreaterThan: null },
        }),
      ),
    )
    renderCheckout()
    const banner = await screen.findByTestId('payment-mode-banner')
    expect(banner).toHaveTextContent(/payment is configured to fail/i)
  })

  it('shows the payment-mode banner with threshold when mode is FailIfAmountGreaterThan', async () => {
    server.use(
      http.get(/\/api\/payments\/mock-mode$/, () =>
        HttpResponse.json({
          success: true,
          data: { mode: 'FailIfAmountGreaterThan', failIfAmountGreaterThan: 50 },
        }),
      ),
    )
    renderCheckout()
    const banner = await screen.findByTestId('payment-mode-banner')
    expect(banner).toHaveTextContent(/\$50\.00/)
  })
})
