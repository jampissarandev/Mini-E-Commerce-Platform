import { useSearchParams, Link } from 'react-router-dom'
import { Eye } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'
import { Pagination } from '@/components/Pagination'
import { useAdminOrders } from '@/lib/useAdminOrders'

const ORDER_STATUSES = ['Pending', 'Paid', 'Shipped', 'Delivered', 'Cancelled']

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

/**
 * Admin order list (Task 16a).
 *
 * Filters live in the URL query string (`?page=&status=&q=&from=&to=`) — the
 * URL is the source of truth, so a filtered view is shareable/bookmarkable and
 * matches the 15a API contract end-to-end (`from`/`to` are ISO-8601 dates).
 */
export function AdminOrders() {
  const [searchParams, setSearchParams] = useSearchParams()

  const page = Math.max(1, Number(searchParams.get('page') ?? 1))
  const status = searchParams.get('status') ?? undefined
  const q = searchParams.get('q') ?? ''
  const from = searchParams.get('from') ?? undefined
  const to = searchParams.get('to') ?? undefined

  const { data, isLoading } = useAdminOrders({
    page,
    pageSize: 10,
    status,
    q: q || undefined,
    from,
    to,
  })

  const orders = data?.data ?? []
  const meta = data?.meta

  const updateParam = (key: string, value: string | undefined) => {
    const next = new URLSearchParams(searchParams)
    if (value === undefined || value === '') {
      next.delete(key)
    } else {
      next.set(key, value)
    }
    // Changing any filter resets pagination to page 1.
    if (key !== 'page') next.delete('page')
    setSearchParams(next)
  }

  const clearDateRange = () => {
    const next = new URLSearchParams(searchParams)
    next.delete('from')
    next.delete('to')
    next.delete('page')
    setSearchParams(next)
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Orders Management</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search"
          placeholder="Search by email, name, or order id..."
          value={q}
          onChange={(e) => updateParam('q', e.target.value)}
          className="h-9 w-full max-w-xs"
          aria-label="Search orders"
        />

        <Select
          value={status ?? 'all'}
          onValueChange={(v) =>
            updateParam('status', v === 'all' ? undefined : v)
          }
        >
          <SelectTrigger className="h-9 w-40" aria-label="Filter by status">
            <SelectValue placeholder="All statuses" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All statuses</SelectItem>
            {ORDER_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="flex items-center gap-2">
          <label htmlFor="from-date" className="text-sm text-muted-foreground">
            From
          </label>
          <Input
            id="from-date"
            type="date"
            value={from ?? ''}
            onChange={(e) => updateParam('from', e.target.value || undefined)}
            className="h-9 w-40"
            aria-label="From date"
          />
          <label htmlFor="to-date" className="text-sm text-muted-foreground">
            To
          </label>
          <Input
            id="to-date"
            type="date"
            value={to ?? ''}
            onChange={(e) => updateParam('to', e.target.value || undefined)}
            className="h-9 w-40"
            aria-label="To date"
          />
          {(from || to) && (
            <Button variant="ghost" size="sm" onClick={clearDateRange}>
              Clear
            </Button>
          )}
        </div>
      </div>

      {/* Table */}
      {isLoading ? (
        <div
          className="flex items-center justify-center py-12"
          role="status"
          aria-label="Loading orders"
        >
          <p className="text-muted-foreground">Loading orders...</p>
        </div>
      ) : orders.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12">
          <p className="text-lg text-muted-foreground">No orders found</p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50 text-left">
                  <th className="px-4 py-3 font-medium">Order</th>
                  <th className="px-4 py-3 font-medium">Customer</th>
                  <th className="px-4 py-3 font-medium text-right">Total</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                  <th className="px-4 py-3 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((order) => (
                  <tr
                    key={order.id}
                    className="border-b last:border-b-0 hover:bg-muted/40"
                  >
                    <td className="px-4 py-3">
                      <Link
                        to={`/admin/orders/${order.id}`}
                        className="font-medium hover:underline"
                      >
                        #{order.id}
                      </Link>
                    </td>
                    <td className="px-4 py-3">{order.customerEmail}</td>
                    <td className="px-4 py-3 text-right">
                      {formatPrice(order.total)}
                    </td>
                    <td className="px-4 py-3">
                      <OrderStatusBadge status={order.status} size="sm" />
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(order.createdAt)}
                    </td>
                    <td className="px-4 py-3">
                      <Link
                        to={`/admin/orders/${order.id}`}
                        aria-label={`View order ${order.id}`}
                        className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                      >
                        <Eye className="h-4 w-4" />
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {meta && meta.totalPages > 1 && (
            <Pagination
              currentPage={meta.page}
              totalPages={meta.totalPages}
              onPageChange={(p) => updateParam('page', String(p))}
            />
          )}
        </>
      )}
    </div>
  )
}
