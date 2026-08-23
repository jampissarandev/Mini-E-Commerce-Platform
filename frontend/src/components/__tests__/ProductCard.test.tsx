import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { ProductCard } from '@/components/ProductCard'
import { server } from '@/test/server'
import { useAuthStore } from '@/lib/auth-store'
import type { ProductListItem } from '@/lib/types'

const mockProduct: ProductListItem = {
  id: 1,
  name: 'Laptop Pro',
  slug: 'laptop-pro',
  price: 1299.99,
  imageUrl: '/images/laptop.jpg',
  categoryName: 'Electronics',
  variantCount: 1,
  variants: [{ id: 10, stock: 5 }],
}

let cartPostBodies: Array<{ productVariantId: number; quantity: number }> = []

function LocationProbe() {
  const { pathname } = useLocation()
  return <div data-testid="location-probe">{pathname}</div>
}

function renderCard(product: ProductListItem = mockProduct) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/products']}>
        <Routes>
          <Route path="/products" element={<ProductCard product={product} />} />
          <Route path="/products/:id" element={<LocationProbe />} />
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
  cartPostBodies = []
  server.resetHandlers()
  server.use(
    http.post(/\/api\/cart\/items$/, async ({ request }) => {
      const body = (await request.json()) as { productVariantId: number; quantity: number }
      cartPostBodies.push(body)
      return HttpResponse.json({
        success: true,
        data: {
          id: 99,
          productId: mockProduct.id,
          productVariantId: body.productVariantId,
          productName: mockProduct.name,
          productSlug: mockProduct.slug,
          size: null,
          color: null,
          imageUrl: mockProduct.imageUrl,
          unitPrice: mockProduct.price,
          quantity: body.quantity,
          subtotal: mockProduct.price * body.quantity,
        },
      })
    }),
  )
})

describe('ProductCard', () => {
  it('renders product name', () => {
    renderCard()
    expect(screen.getByText('Laptop Pro')).toBeInTheDocument()
  })

  it('renders formatted price', () => {
    renderCard()
    expect(screen.getByText('$1,299.99')).toBeInTheDocument()
  })

  it('renders category name', () => {
    renderCard()
    expect(screen.getByText('Electronics')).toBeInTheDocument()
  })

  it('renders product image with alt text', () => {
    renderCard()
    const img = screen.getByRole('img', { name: /laptop pro/i })
    expect(img).toBeInTheDocument()
    expect(img).toHaveAttribute('src', '/images/laptop.jpg')
  })

  it('links to the product detail page', () => {
    renderCard()
    const link = screen.getByRole('link', { name: /laptop pro/i })
    expect(link).toHaveAttribute('href', '/products/1')
  })

  it('handles missing image by showing placeholder', () => {
    renderCard({ ...mockProduct, imageUrl: '' })
    expect(screen.getByText(/no image/i)).toBeInTheDocument()
  })

  it('shows variant count when product has multiple variants', () => {
    renderCard({ ...mockProduct, variantCount: 3 })
    expect(screen.getByText(/3 variants/i)).toBeInTheDocument()
  })

  it('does not show variant count when only one variant', () => {
    renderCard({ ...mockProduct, variantCount: 1 })
    expect(screen.queryByText(/variants/i)).not.toBeInTheDocument()
  })

  // ─────────────── Quick-add (ADR 0008 Phase C) ───────────────

  it('shows a single "Add to cart" button on multi-variant cards', () => {
    renderCard({ ...mockProduct, variantCount: 3 })
    expect(screen.getByRole('button', { name: /add to cart/i })).toBeInTheDocument()
    expect(screen.queryByText(/view options/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/view product/i)).not.toBeInTheDocument()
  })

  it('shows a single "Add to cart" button on single-variant cards', () => {
    renderCard({ ...mockProduct, variantCount: 1 })
    expect(screen.getByRole('button', { name: /add to cart/i })).toBeInTheDocument()
  })

  it('quick-add sends the preferred variant id with quantity 1', async () => {
    const user = userEvent.setup()
    renderCard({ ...mockProduct, variantCount: 1 })

    await user.click(screen.getByRole('button', { name: /add to cart/i }))

    await waitFor(() => {
      expect(cartPostBodies).toHaveLength(1)
    })
    expect(cartPostBodies[0]).toEqual({ productVariantId: 10, quantity: 1 })
  })

  it('quick-add on a multi-variant card picks the first in-stock variant', async () => {
    const user = userEvent.setup()
    // First variant OOS, second in stock — pickPreferredVariant must skip
    // the OOS one; the wire payload is the source of truth for which won.
    renderCard({
      ...mockProduct,
      variantCount: 2,
      variants: [
        { id: 10, stock: 0 },
        { id: 11, stock: 7 },
      ],
    })

    await user.click(screen.getByRole('button', { name: /add to cart/i }))

    await waitFor(() => {
      expect(cartPostBodies).toHaveLength(1)
    })
    expect(cartPostBodies[0]).toEqual({ productVariantId: 11, quantity: 1 })
  })

  it('quick-add when all variants are out of stock navigates to the detail page instead', async () => {
    const user = userEvent.setup()
    renderCard({
      ...mockProduct,
      variantCount: 2,
      variants: [
        { id: 10, stock: 0 },
        { id: 11, stock: 0 },
      ],
    })

    await user.click(screen.getByRole('button', { name: /add to cart/i }))

    // Navigated to detail (probe shows /products/1) and nothing was added.
    await waitFor(() => {
      expect(screen.getByTestId('location-probe')).toHaveTextContent('/products/1')
    })
    expect(cartPostBodies).toHaveLength(0)
  })
})
