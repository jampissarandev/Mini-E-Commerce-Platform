import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type {
  ApiResponse,
  DashboardSummary,
  SalesPoint,
  LowStockProductDto,
  AdminOrderListItem,
} from './types'

/**
 * Fetch the headline KPI metrics for the admin dashboard (Task 17a).
 * Query key is stable: `['admin-dashboard-summary']`.
 */
export function useAdminDashboardSummary() {
  return useQuery({
    queryKey: ['admin-dashboard-summary'],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<DashboardSummary>>(
        '/admin/dashboard/summary',
      )
      return data
    },
  })
}

/**
 * Fetch the daily sales series for the last `days` calendar days (Task 17a).
 * Query key is per-days: `['admin-dashboard-sales', days]`.
 */
export function useAdminDashboardSales(days = 30) {
  return useQuery({
    queryKey: ['admin-dashboard-sales', days],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<SalesPoint[]>>(
        '/admin/dashboard/sales',
        { params: { days } },
      )
      return data
    },
  })
}

/**
 * Fetch the latest orders, newest-first (Task 17a).
 * Query key is per-limit: `['admin-dashboard-recent-orders', limit]`.
 */
export function useAdminDashboardRecentOrders(limit = 10) {
  return useQuery({
    queryKey: ['admin-dashboard-recent-orders', limit],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AdminOrderListItem[]>>(
        '/admin/dashboard/recent-orders',
        { params: { limit } },
      )
      return data
    },
  })
}

/**
 * Fetch products at or below a stock threshold (Task 17a).
 * Query key is per-threshold: `['admin-dashboard-low-stock', threshold]`.
 */
export function useAdminDashboardLowStock(threshold = 10) {
  return useQuery({
    queryKey: ['admin-dashboard-low-stock', threshold],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<LowStockProductDto[]>>(
        '/admin/dashboard/low-stock',
        { params: { threshold } },
      )
      return data
    },
  })
}