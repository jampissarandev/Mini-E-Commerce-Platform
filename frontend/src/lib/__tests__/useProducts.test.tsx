import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { useProducts, useProduct, useCategories } from '@/lib/useProducts'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    )
  }
}

const mockProductsResponse = {
  success: true,
  data: [
    { id: 1, name: 'Laptop', slug: 'laptop', price: 999.99, imageUrl: '/images/laptop.jpg', categoryName: 'Electronics' },
    { id: 2, name: 'Headphones', slug: 'headphones', price: 49.99, imageUrl: '/images/headphones.jpg', categoryName: 'Electronics' },
  ],
  meta: { page: 1, pageSize: 10, totalCount: 2, totalPages: 1 },
}

const mockProductDetailResponse = {
  success: true,
  data: {
    id: 1,
    name: 'Laptop',
    slug: 'laptop',
    description: 'A powerful laptop',
    price: 999.99,
    stock: 10,
    createdAt: '2026-01-01T00:00:00Z',
    category: { id: 1, name: 'Electronics', slug: 'electronics' },
    images: [{ id: 1, url: '/images/laptop.jpg', sortOrder: 0 }],
  },
}

const mockCategoriesResponse = {
  success: true,
  data: [
    { id: 1, name: 'Electronics', slug: 'electronics', productCount: 5 },
    { id: 2, name: 'Books', slug: 'books', productCount: 3 },
  ],
}

describe('useProducts', () => {
  const wrapper = createWrapper()

  beforeEach(() => {
    server.resetHandlers()
  })

  it('fetches paginated product list', async () => {
    server.use(
      http.get(/\/api\/products$/, () =>
        HttpResponse.json(mockProductsResponse),
      ),
    )

    const { result } = renderHook(() => useProducts(), { wrapper })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data).toHaveLength(2)
    expect(result.current.data?.meta?.totalCount).toBe(2)
  })

  it('passes query parameters to the API', async () => {
    let capturedUrl = ''

    server.use(
      http.get(/\/api\/products$/, ({ request }) => {
        capturedUrl = request.url
        return HttpResponse.json(mockProductsResponse)
      }),
    )

    renderHook(
      () => useProducts({ page: 2, pageSize: 5, category: 'electronics', search: 'laptop', sort: 'price_asc' }),
      { wrapper },
    )

    await waitFor(() => {
      expect(capturedUrl).toContain('page=2')
      expect(capturedUrl).toContain('pageSize=5')
      expect(capturedUrl).toContain('category=electronics')
      expect(capturedUrl).toContain('search=laptop')
      expect(capturedUrl).toContain('sort=price_asc')
    })
  })

  it('handles error response', async () => {
    server.use(
      http.get(/\/api\/products$/, () =>
        HttpResponse.json({ success: false, error: { code: 'ERROR', message: 'Server error' } }, { status: 500 }),
      ),
    )

    const { result } = renderHook(() => useProducts(), { wrapper })

    await waitFor(() => {
      expect(result.current.isError).toBe(true)
    })
  })
})

describe('useProduct', () => {
  const wrapper = createWrapper()

  beforeEach(() => {
    server.resetHandlers()
  })

  it('fetches a single product by id', async () => {
    server.use(
      http.get(/\/api\/products\/1$/, () =>
        HttpResponse.json(mockProductDetailResponse),
      ),
    )

    const { result } = renderHook(() => useProduct(1), { wrapper })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data?.name).toBe('Laptop')
    expect(result.current.data?.data?.stock).toBe(10)
  })

  it('does not fetch when id is 0', () => {
    server.use(
      http.get(/\/api\/products\/0$/, () =>
        HttpResponse.json({ success: false, error: { code: 'NOT_FOUND', message: 'Not found' } }, { status: 404 }),
      ),
    )

    const { result } = renderHook(() => useProduct(0), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
  })
})

describe('useCategories', () => {
  const wrapper = createWrapper()

  beforeEach(() => {
    server.resetHandlers()
  })

  it('fetches category list', async () => {
    server.use(
      http.get(/\/api\/categories$/, () =>
        HttpResponse.json(mockCategoriesResponse),
      ),
    )

    const { result } = renderHook(() => useCategories(), { wrapper })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data).toHaveLength(2)
    expect(result.current.data?.data?.[0]?.name).toBe('Electronics')
  })
})
