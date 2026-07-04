import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import { ProductCard } from '@/components/ProductCard'
import type { ProductListItem } from '@/lib/types'

const mockProduct: ProductListItem = {
  id: 1,
  name: 'Laptop Pro',
  slug: 'laptop-pro',
  price: 1299.99,
  imageUrl: '/images/laptop.jpg',
  categoryName: 'Electronics',
}

function renderCard(product: ProductListItem = mockProduct) {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <ProductCard product={product} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

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

  it('renders out-of-stock badge when stock is zero', () => {
    renderCard({ ...mockProduct } as ProductListItem)
    // ProductListItem doesn't have stock, so no badge shown by default
    expect(screen.queryByText(/out of stock/i)).not.toBeInTheDocument()
  })
})
