import { Link } from 'react-router-dom'
import { ArrowRight } from 'lucide-react'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'
import type { AdminOrderListItem } from '@/lib/types'
import { formatCurrency } from '@/lib/utils'

interface RecentOrdersListProps {
  orders: AdminOrderListItem[]
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  })
}

/**
 * Recent-orders list for the admin dashboard (Task 17c). Renders up to 10
 * rows with status badges, each linking to the order detail page. The parent
 * owns loading and empty states.
 */
export function RecentOrdersList({ orders }: RecentOrdersListProps) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50 text-left">
            <th className="px-4 py-3 font-medium">Order</th>
            <th className="px-4 py-3 font-medium">Customer</th>
            <th className="px-4 py-3 font-medium text-right">Total</th>
            <th className="px-4 py-3 font-medium">Status</th>
            <th className="px-4 py-3 font-medium">Date</th>
            <th className="px-4 py-3 font-medium" aria-label="Actions" />
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
                {formatCurrency(order.total)}
              </td>
              <td className="px-4 py-3">
                <OrderStatusBadge status={order.status} size="sm" />
              </td>
              <td className="px-4 py-3 text-muted-foreground">
                {formatDate(order.createdAt)}
              </td>
              <td className="px-4 py-3 text-right">
                <Link
                  to={`/admin/orders/${order.id}`}
                  aria-label={`View order ${order.id}`}
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                >
                  <ArrowRight className="h-4 w-4" />
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}