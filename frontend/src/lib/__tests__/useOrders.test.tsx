import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/server'
import { useOrders, useOrder, useCheckout } from '@/lib/useOrders'
import { type ReactNode } from 'react'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

const mockOrderDto = {
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
    {
      id: 1,
      productId: 1,
      productName: 'Laptop Pro',
      unitPrice: 1299.99,
      quantity: 1,
      subtotal: 1299.99,
    },
  ],
}

describe('useOrders', () => {
  it('fetches paginated orders', async () => {
    server.use(
      http.get(/\/api\/orders$/, () =>
        HttpResponse.json({
          success: true,
          data: [mockOrderDto],
          meta: { page: 1, pageSize: 10, totalCount: 1, totalPages: 1 },
        }),
      ),
    )

    const { result } = renderHook(() => useOrders(10), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data).toHaveLength(1)
    expect(result.current.data?.data?.[0]?.id).toBe(1)
    expect(result.current.data?.meta?.totalCount).toBe(1)
  })

  it('returns empty list when no orders exist', async () => {
    const { result } = renderHook(() => useOrders(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data).toHaveLength(0)
  })
})

describe('useOrder', () => {
  it('fetches a single order by id', async () => {
    server.use(
      http.get(/\/api\/orders\/1$/, () =>
        HttpResponse.json({
          success: true,
          data: mockOrderDto,
        }),
      ),
    )

    const { result } = renderHook(() => useOrder(1), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data?.id).toBe(1)
    expect(result.current.data?.data?.status).toBe('Paid')
    expect(result.current.data?.data?.items).toHaveLength(1)
  })

  it('does not fetch when id is 0', () => {
    const { result } = renderHook(() => useOrder(0), { wrapper: createWrapper() })

    expect(result.current.isFetching).toBe(false)
    expect(result.current.isPending).toBe(true)
  })
})

describe('useCheckout', () => {
  it('creates an order and returns the order dto', async () => {
    server.use(
      http.post(/\/api\/orders$/, () =>
        HttpResponse.json(
          { success: true, data: mockOrderDto },
          { status: 201 },
        ),
      ),
    )

    const { result } = renderHook(() => useCheckout(), { wrapper: createWrapper() })

    result.current.mutate({
      fullName: 'Jane Doe',
      street: '123 Main St',
      city: 'Springfield',
      postalCode: '62701',
      country: 'USA',
      phone: '+1-555-0100',
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data?.id).toBe(1)
    expect(result.current.data?.data?.status).toBe('Paid')
  })

  it('exposes error on failure', async () => {
    server.use(
      http.post(/\/api\/orders$/, () =>
        HttpResponse.json(
          { success: false, error: { code: 'EMPTY_CART', message: 'Your cart is empty.' } },
          { status: 400 },
        ),
      ),
    )

    const { result } = renderHook(() => useCheckout(), { wrapper: createWrapper() })

    result.current.mutate({
      fullName: 'Jane Doe',
      street: '123 Main St',
      city: 'Springfield',
      postalCode: '62701',
      country: 'USA',
      phone: '+1-555-0100',
    })

    await waitFor(() => expect(result.current.isError).toBe(true))

    expect(result.current.error).toBeDefined()
  })
})
