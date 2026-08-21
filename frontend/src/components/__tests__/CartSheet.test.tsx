import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { CartSheet } from '@/components/CartSheet'

function renderSheet(open = true) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CartSheet open={open} onOpenChange={() => {}} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

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
        productVariantId: 10,
        productName: 'Laptop Pro',
        productSlug: 'laptop-pro',
        size: 'M',
        color: 'Black',
        imageUrl: '/images/laptop.jpg',
        unitPrice: 1299.99,
        quantity: 2,
        subtotal: 2599.98,
      },
      {
        id: 2,
        productId: 2,
        productVariantId: 11,
        productName: 'Wireless Mouse',
        productSlug: 'wireless-mouse',
        size: null,
        color: null,
        imageUrl: '/images/mouse.jpg',
        unitPrice: 29.99,
        quantity: 1,
        subtotal: 29.99,
      },
    ],
    total: 2629.97,
  },
}

const emptyCartDto = {
  success: true,
  data: {
    id: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [],
    total: 0,
  },
}

describe('CartSheet', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders cart heading', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    expect(screen.getByRole('heading', { name: /shopping cart/i })).toBeInTheDocument()
  })

  it('renders cart items with names and prices', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    const laptop = await screen.findByText('Laptop Pro')
    const mouse = screen.getByText('Wireless Mouse')
    expect(laptop).toBeInTheDocument()
    expect(mouse).toBeInTheDocument()
  })

  it('displays the correct total', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    await screen.findByText('Laptop Pro')
    expect(screen.getByText(/\$2,629\.97/)).toBeInTheDocument()
  })

  it('shows empty cart message when no items', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(emptyCartDto)),
    )
    renderSheet()
    await screen.findByText(/your cart is empty/i)
  })

  it('renders remove buttons for each cart item', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    await screen.findByText('Laptop Pro')
    const removeButtons = screen.getAllByRole('button', { name: /remove/i })
    expect(removeButtons).toHaveLength(2)
  })

  it('renders quantity controls for each cart item', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    await screen.findByText('Laptop Pro')
    const decreaseButtons = screen.getAllByRole('button', { name: /decrease quantity/i })
    const increaseButtons = screen.getAllByRole('button', { name: /increase quantity/i })
    expect(decreaseButtons).toHaveLength(2)
    expect(increaseButtons).toHaveLength(2)
  })

  it('calls onOpenChange when close button is clicked', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    let openState = true
    const handleOpenChange = (open: boolean) => { openState = open }
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    })
    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <CartSheet open={openState} onOpenChange={handleOpenChange} />
        </MemoryRouter>
      </QueryClientProvider>,
    )
    await screen.findByText('Laptop Pro')
    const closeButton = screen.getByRole('button', { name: /close/i })
    await userEvent.click(closeButton)
    expect(openState).toBe(false)
  })

  it('links each cart item to /products/<id> using the numeric product id, not the slug', async () => {
    // Regression for the 27d slug-in-route defect: the cart link passed
    // item.productSlug into /products/:id, which ProductDetail.tsx Number()-
    // coerces to NaN, breaking navigation from the cart to product detail.
    // The product slug ("laptop-pro") must NOT appear in the link href.
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    renderSheet()
    const laptopLink = await screen.findByRole('link', { name: 'Laptop Pro' })
    const mouseLink = screen.getByRole('link', { name: 'Wireless Mouse' })
    expect(laptopLink).toHaveAttribute('href', '/products/1')
    expect(mouseLink).toHaveAttribute('href', '/products/2')
  })
})
