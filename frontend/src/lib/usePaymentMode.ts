import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { ApiResponse, MockPaymentModeDto } from './types'

const PAYMENT_MODE_KEY = ['payments', 'mockMode'] as const

/**
 * Fetches the currently active mock payment mode. The result is used to show
 * a dev-only banner on the checkout page so demos can clearly see whether
 * the next checkout will succeed or fail.
 *
 * The endpoint is public and never returns sensitive data, so we don't
 * gate this on auth state.
 */
export function usePaymentMode() {
  return useQuery({
    queryKey: PAYMENT_MODE_KEY,
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<MockPaymentModeDto>>('/payments/mock-mode')
      return data
    },
    staleTime: 30_000,
  })
}
