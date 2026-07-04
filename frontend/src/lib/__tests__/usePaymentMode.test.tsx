import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import type { ReactNode } from 'react'
import { server } from '@/test/server'
import { usePaymentMode } from '@/lib/usePaymentMode'

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('usePaymentMode', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  it('returns the current mock payment mode', async () => {
    server.use(
      http.get(/\/api\/payments\/mock-mode$/, () =>
        HttpResponse.json({
          success: true,
          data: { mode: 'AlwaysFail', failIfAmountGreaterThan: null },
        }),
      ),
    )

    const { result } = renderHook(() => usePaymentMode(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data.mode).toBe('AlwaysFail')
  })

  it('returns the threshold in FailIfAmountGreaterThan mode', async () => {
    server.use(
      http.get(/\/api\/payments\/mock-mode$/, () =>
        HttpResponse.json({
          success: true,
          data: { mode: 'FailIfAmountGreaterThan', failIfAmountGreaterThan: 250 },
        }),
      ),
    )

    const { result } = renderHook(() => usePaymentMode(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(result.current.data?.data.failIfAmountGreaterThan).toBe(250)
  })
})
