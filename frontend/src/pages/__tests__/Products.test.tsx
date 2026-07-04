import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { Products } from '@/pages/Products'

function renderProducts(route = '/products') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <Products />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const productsPage1 = {
  success: true,
  data: [
    { id: 1, name: 'Laptop', slug: 'laptop', price: 999.99, imageUrl: '/images/laptop.jpg', categoryName: 'Electronics' },
    { id: 2, name: 'Headphones', slug: 'headphones', price: 49.99, imageUrl: '/images/headphones.jpg', categoryName: 'Electronics' },
  ],
  meta: { page: 1, pageSize: 10, totalCount: 15, totalPages: 2 },
}

const categoriesData = {
  success: true,
  data: [
    { id: 1, name: 'Electronics', slug: 'electronics', productCount: 2 },
    { id: 2, name: 'Books', slug: 'books', productCount: 1 },
  ],
}

describe('Products page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders product cards from API data', async () => {
    server.use(
      http.get(/\/api\/products$/, () => HttpResponse.json(productsPage1)),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts()

    await waitFor(() => {
      expect(screen.getByText('Laptop')).toBeInTheDocument()
    })
    expect(screen.getByText('Headphones')).toBeInTheDocument()
    expect(screen.getByText('$999.99')).toBeInTheDocument()
    expect(screen.getByText('$49.99')).toBeInTheDocument()
  })

  it('renders category filter buttons', async () => {
    server.use(
      http.get(/\/api\/products$/, () => HttpResponse.json(productsPage1)),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts()

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /electronics/i })).toBeInTheDocument()
    })
    expect(screen.getByRole('button', { name: /books/i })).toBeInTheDocument()
  })

  it('renders search input', async () => {
    server.use(
      http.get(/\/api\/products$/, () => HttpResponse.json(productsPage1)),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts()

    await waitFor(() => {
      expect(screen.getByRole('searchbox', { name: /search products/i })).toBeInTheDocument()
    })
  })

  it('shows loading state while fetching', () => {
    server.use(
      http.get(/\/api\/products$/, () => HttpResponse.json(productsPage1)),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts()

    // Skeleton loaders should be present while loading
    expect(screen.getByText(/loading products/i)).toBeInTheDocument()
  })

  it('shows empty state when no products match', async () => {
    server.use(
      http.get(/\/api\/products$/, () =>
        HttpResponse.json({ success: true, data: [], meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }),
      ),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts('/products?search=nonexistent')

    await waitFor(() => {
      expect(screen.getByText(/no products found/i)).toBeInTheDocument()
    })
  })

  it('shows error state on API failure', async () => {
    server.use(
      http.get(/\/api\/products$/, () =>
        HttpResponse.json({ success: false, error: { code: 'ERROR', message: 'Server error' } }, { status: 500 }),
      ),
      http.get(/\/api\/categories$/, () => HttpResponse.json(categoriesData)),
    )

    renderProducts()

    await waitFor(() => {
      expect(screen.getByText(/something went wrong/i)).toBeInTheDocument()
    })
  })
})
