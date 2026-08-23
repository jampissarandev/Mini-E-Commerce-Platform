import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse, delay } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { useCart, useAddToCart, useUpdateCartItem, useRemoveCartItem, useClearCart } from '@/lib/useCart'
import type { ApiResponse, CartDto } from '@/lib/types'
import type { ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
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
    ],
    total: 2599.98,
  },
}

const mockCartItemDto = {
  success: true,
  data: {
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
}

describe('useCart', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('fetches the current user cart', async () => {
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
    const { result } = renderHook(() => useCart(), { wrapper: createWrapper() })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data.items).toHaveLength(1)
    expect(result.current.data?.data.total).toBe(2599.98)
  })
})

describe('useAddToCart', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('adds an item to the cart', async () => {
    server.use(
      http.post(/\/api\/cart\/items$/, () => HttpResponse.json(mockCartItemDto)),
    )
    const { result } = renderHook(() => useAddToCart(), { wrapper: createWrapper() })

    result.current.mutate({ productVariantId: 11, quantity: 1 })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data.productName).toBe('Wireless Mouse')
  })
})

describe('useUpdateCartItem', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('updates a cart item quantity', async () => {
    const updatedDto = {
      success: true,
      data: { ...mockCartItemDto.data, quantity: 3, subtotal: 89.97 },
    }
    server.use(
      http.put(/\/api\/cart\/items\/1$/, () => HttpResponse.json(updatedDto)),
    )
    const { result } = renderHook(() => useUpdateCartItem(), { wrapper: createWrapper() })

    result.current.mutate({ id: 1, quantity: 3 })

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })

    expect(result.current.data?.data.quantity).toBe(3)
  })
})

describe('useRemoveCartItem', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('removes a cart item', async () => {
    server.use(
      http.delete(/\/api\/cart\/items\/1$/, () =>
        HttpResponse.json({ success: true }),
      ),
    )
    const { result } = renderHook(() => useRemoveCartItem(), { wrapper: createWrapper() })

    result.current.mutate(1)

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })
  })
})

describe('useClearCart', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('clears all cart items', async () => {
    server.use(
      http.delete(/\/api\/cart$/, () =>
        HttpResponse.json({ success: true }),
      ),
    )
    const { result } = renderHook(() => useClearCart(), { wrapper: createWrapper() })

    result.current.mutate()

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true)
    })
  })
})

// ─────────────── Optimistic add (ADR 0008 Phase C, Q6) ───────────────

describe('useAddToCart optimistic update', () => {
  beforeEach(() => {
    server.resetHandlers()
    // Default cart: one item, quantity 2 → badge count starts at 2.
    server.use(
      http.get(/\/api\/cart$/, () => HttpResponse.json(mockCartDto)),
    )
  })

  function renderCartWithAddToCart() {
    return renderHook(
      () => {
        const cart = useCart()
        const addToCart = useAddToCart()
        return { cart, addToCart }
      },
      { wrapper: createWrapper() },
    )
  }

  function itemCount(result: { cart: { data?: ApiResponse<CartDto> } }): number {
    return (
      result.cart.data?.data.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0
    )
  }

  it('bumps the cached cart count immediately via onMutate, before the POST resolves', async () => {
    // Slow POST: the optimistic patch must land while it is still pending.
    server.use(
      http.post(/\/api\/cart\/items$/, async () => {
        await delay(300)
        return HttpResponse.json(mockCartItemDto)
      }),
    )

    const { result } = renderCartWithAddToCart()

    await waitFor(() => {
      expect(result.current.cart.isSuccess).toBe(true)
    })
    expect(itemCount(result.current)).toBe(2)

    result.current.addToCart.mutate({ productVariantId: 11, quantity: 3 })

    // Optimistic: count shows 2 + 3 = 5 while the POST is in flight.
    await waitFor(
      () => {
        expect(itemCount(result.current)).toBe(5)
      },
      { timeout: 1000 },
    )

    // After the POST resolves and the invalidation refetch lands, the cache
    // reflects the server response (mockCartItemDto replaces the seed).
    await waitFor(() => {
      expect(result.current.addToCart.isSuccess).toBe(true)
    })
  })

  it('rolls back the optimistic bump when the POST fails', async () => {
    server.use(
      http.post(/\/api\/cart\/items$/, async () => {
        await delay(300)
        return HttpResponse.json(
          { success: false, error: { code: 'INSUFFICIENT_STOCK', message: 'nope' } },
          { status: 400 },
        )
      }),
    )

    const { result } = renderCartWithAddToCart()

    await waitFor(() => {
      expect(result.current.cart.isSuccess).toBe(true)
    })

    result.current.addToCart.mutate({ productVariantId: 11, quantity: 3 })

    // Optimistic bump visible while the request is in flight…
    await waitFor(
      () => {
        expect(itemCount(result.current)).toBe(5)
      },
      { timeout: 1000 },
    )

    // …and rolled back to the pre-mutation snapshot once it fails.
    await waitFor(() => {
      expect(result.current.addToCart.isError).toBe(true)
    })
    expect(itemCount(result.current)).toBe(2)
  })
})
