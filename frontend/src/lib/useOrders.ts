import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type { ApiResponse, OrderDto, CheckoutRequest } from './types'

const ORDERS_KEY = ['orders']

/** Create an order from the current cart (checkout). */
export function useCheckout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (request: CheckoutRequest) => {
      const { data } = await api.post<ApiResponse<OrderDto>>('/orders', request)
      return data
    },
    onSuccess: () => {
      // Invalidate both cart (it's now empty) and orders list
      queryClient.invalidateQueries({ queryKey: ['cart'] })
      queryClient.invalidateQueries({ queryKey: ORDERS_KEY })
    },
  })
}

/**
 * Fetch paginated orders for the current user.
 * Returns the underlying query (with `data?.meta`) plus a `goToPage` helper.
 */
export function useOrders(pageSize = 10) {
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: [...ORDERS_KEY, page, pageSize],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<OrderDto[]>>('/orders', {
        params: { page, pageSize },
      })
      return data
    },
  })
  return {
    ...query,
    page,
    totalPages: query.data?.meta?.totalPages ?? 0,
    goToPage: setPage,
  }
}

/** Fetch a single order by ID. */
export function useOrder(id: number) {
  return useQuery({
    queryKey: [...ORDERS_KEY, id],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<OrderDto>>(`/orders/${id}`)
      return data
    },
    enabled: id > 0,
  })
}
