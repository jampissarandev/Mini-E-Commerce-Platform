import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { ProductDetail } from '@/pages/ProductDetail'

function renderDetail(id = '1') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/products/${id}`]}>
        <Routes>
          <Route path="/products/:id" element={<ProductDetail />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

const mockProduct = {
  success: true,
  data: {
    id: 1,
    name: 'Laptop Pro',
    slug: 'laptop-pro',
    description: 'A powerful laptop for professionals.',
    price: 1299.99,
    stock: 10,
    createdAt: '2026-01-01T00:00:00Z',
    category: { id: 1, name: 'Electronics', slug: 'electronics' },
    images: [
      { id: 1, url: '/images/laptop1.jpg', sortOrder: 0 },
      { id: 2, url: '/images/laptop2.jpg', sortOrder: 1 },
    ],
  },
}

describe('ProductDetail page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('renders product name', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /laptop pro/i })).toBeInTheDocument()
    })
  })

  it('renders product price', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      expect(screen.getByText('$1,299.99')).toBeInTheDocument()
    })
  })

  it('renders product description', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      expect(screen.getByText(/powerful laptop/i)).toBeInTheDocument()
    })
  })

  it('renders category name', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      expect(screen.getByText('Electronics')).toBeInTheDocument()
    })
  })

  it('renders stock information', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      expect(screen.getByText(/10 in stock/i)).toBeInTheDocument()
    })
  })

  it('renders product images', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    await waitFor(() => {
      const images = screen.getAllByRole('img', { name: /laptop pro/i })
      // 1 main image + 2 thumbnails = 3 total
      expect(images).toHaveLength(3)
    })
  })

  it('shows 404 state for non-existent product', async () => {
    server.use(
      http.get(/\/api\/products\/999$/, () =>
        HttpResponse.json({ success: false, error: { code: 'NOT_FOUND', message: 'Not found' } }, { status: 404 }),
      ),
    )
    renderDetail('999')
    await waitFor(() => {
      expect(screen.getByText(/product not found/i)).toBeInTheDocument()
    })
  })

  it('shows loading state', () => {
    server.use(
      http.get(/\/api\/products\/1$/, () => HttpResponse.json(mockProduct)),
    )
    renderDetail()
    expect(screen.getByText(/loading product/i)).toBeInTheDocument()
  })
})
