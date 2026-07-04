import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { CartBadge } from '@/components/CartBadge'

function renderBadge() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CartBadge />
      </MemoryRouter>
    </QueryClientProvider>,
  )
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

const cartWith3Items = {
  success: true,
  data: {
    id: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: [
      { id: 1, productId: 1, productName: 'A', productSlug: 'a', imageUrl: '', unitPrice: 10, quantity: 1, subtotal: 10 },
      { id: 2, productId: 2, productName: 'B', productSlug: 'b', imageUrl: '', unitPrice: 20, quantity: 2, subtotal: 40 },
      { id: 3, productId: 3, productName: 'C', productSlug: 'c', imageUrl: '', unitPrice: 30, quantity: 1, subtotal: 30 },
    ],
    total: 80,
  },
}

const cartWith10PlusItems = {
  success: true,
  data: {
    id: 1,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    items: Array.from({ length: 5 }, (_, i) => ({
      id: i + 1,
      productId: i + 1,
      productName: `Item ${i + 1}`,
      productSlug: `item-${i + 1}`,
      imageUrl: '',
      unitPrice: 10,
      quantity: 3,
      subtotal: 30,
    })),
    total: 150,
  },
}

describe('CartBadge', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('hides badge when cart is empty', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(emptyCartDto)),
    )
    renderBadge()
    // The badge element should not be present when cart is empty
    await screen.findByRole('button', { name: /open cart/i })
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('displays the total item count', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(cartWith3Items)),
    )
    renderBadge()
    // Total quantity: 1 + 2 + 1 = 4
    expect(await screen.findByText('4')).toBeInTheDocument()
  })

  it('displays "9+" when total exceeds 9', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(cartWith10PlusItems)),
    )
    renderBadge()
    // Total quantity: 5 * 3 = 15
    expect(await screen.findByText('9+')).toBeInTheDocument()
  })

  it('renders the shopping cart icon', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(emptyCartDto)),
    )
    renderBadge()
    expect(await screen.findByRole('button', { name: /open cart/i })).toBeInTheDocument()
  })
})
