import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type {
  ApiResponse,
  AdminOrderListItem,
  AdminOrderDetail,
  UpdateOrderStatusRequest,
} from './types'

export interface AdminOrderListParams {
  page?: number
  pageSize?: number
  status?: string
  q?: string
  from?: string
  to?: string
}

/**
 * Fetch a paginated, filterable list of every order (admin-only).
 * Query key is per-filter: `['admin-orders', filters]` so each distinct
 * filter combination gets its own cache entry (16a).
 */
export function useAdminOrders(params: AdminOrderListParams = {}) {
  return useQuery({
    queryKey: ['admin-orders', params],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AdminOrderListItem[]>>(
        '/admin/orders',
        { params },
      )
      return data
    },
  })
}

/**
 * Fetch a single order's full detail (admin-only).
 * Query key is per-id: `['admin-order', id]`. Requests `?include=allowedNexts`
 * so the response carries the valid next statuses for the status dropdown (16b).
 */
export function useAdminOrder(id: number) {
  return useQuery({
    queryKey: ['admin-order', id],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AdminOrderDetail>>(
        `/admin/orders/${id}`,
        { params: { include: 'allowedNexts' } },
      )
      return data
    },
    enabled: id > 0,
  })
}

/**
 * Update an order's status (admin-only).
 *
 * On success the `['admin-order', id]` cache entry is patched with the server
 * response (which carries the new `Status` + `AllowedNextStatuses`), so the
 * detail page flips both without a refetch round-trip. The `['admin-orders']`
 * prefix and the `['admin-order', id]` entry are then invalidated so a later
 * navigation re-fetches the authoritative value (16c).
 */
export function useAdminOrderStatusUpdate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, status }: { id: number; status: string }) => {
      const { data } = await api.put<ApiResponse<AdminOrderDetail>>(
        `/admin/orders/${id}/status`,
        { status } satisfies UpdateOrderStatusRequest,
      )
      return data
    },
    onSuccess: (data, variables) => {
      queryClient.setQueryData(['admin-order', variables.id], data)
      queryClient.invalidateQueries({ queryKey: ['admin-orders'] })
      queryClient.invalidateQueries({
        queryKey: ['admin-order', variables.id],
      })
    },
  })
}