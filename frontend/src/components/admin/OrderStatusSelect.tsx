import { useState } from 'react'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useAdminOrderStatusUpdate } from '@/lib/useAdminOrders'
import type { AdminOrderDetail } from '@/lib/types'

interface OrderStatusSelectProps {
  order: AdminOrderDetail
  /** Invoked after a status update succeeds (e.g. to refresh the list). */
  onUpdated?: () => void
}

function extractErrorMessage(error: unknown): string {
  if (
    error &&
    typeof error === 'object' &&
    'response' in error &&
    error.response &&
    typeof error.response === 'object' &&
    'data' in error.response
  ) {
    const data = error.response.data as { error?: { message?: string } }
    return data.error?.message ?? 'Failed to update status. Please try again.'
  }
  return 'An unexpected error occurred. Please try again.'
}

/**
 * Reusable status-update dropdown (Task 16c).
 *
 * Renders only the valid next statuses from `order.allowedNextStatuses` — the
 * server-provided array from `OrderStatusTransitions.AllowedNexts` is the single
 * source of truth, so invalid transitions are never offered as options.
 *
 * On change it calls `PUT /admin/orders/:id/status`; the mutation hook patches
 * the `['admin-order', id]` cache entry with the server response (flipping
 * `Status` + `AllowedNextStatuses` without a refetch round-trip) and invalidates
 * the `['admin-orders']` prefix + the detail entry.
 */
export function OrderStatusSelect({ order, onUpdated }: OrderStatusSelectProps) {
  const updateStatus = useAdminOrderStatusUpdate()
  const [error, setError] = useState<string | null>(null)

  const allowed = order.allowedNextStatuses ?? []

  const handleChange = (status: string) => {
    setError(null)
    updateStatus.mutate(
      { id: order.id, status },
      {
        onSuccess: () => onUpdated?.(),
        onError: (err) => setError(extractErrorMessage(err)),
      },
    )
  }

  if (allowed.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        No further transitions available.
      </p>
    )
  }

  return (
    <div className="space-y-1">
      <Select
        value={undefined}
        onValueChange={handleChange}
        disabled={updateStatus.isPending}
      >
        <SelectTrigger className="h-9 w-44" aria-label="Update order status">
          <SelectValue placeholder="Update status..." />
        </SelectTrigger>
        <SelectContent>
          {allowed.map((s) => (
            <SelectItem key={s} value={s}>
              {s}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {updateStatus.isPending && (
        <p className="text-xs text-muted-foreground">Updating...</p>
      )}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </div>
  )
}