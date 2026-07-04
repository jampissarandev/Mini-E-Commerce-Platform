import { Link } from 'react-router-dom'
import { Package, ArrowRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'
import { Pagination } from '@/components/Pagination'
import { useOrders } from '@/lib/useOrders'

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

export function OrderHistory() {
  const { data, isLoading, page, totalPages, goToPage } = useOrders()

  const orders = data?.data ?? []

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading orders">
        <p className="text-muted-foreground">Loading orders...</p>
      </div>
    )
  }

  if (orders.length === 0) {
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold">My Orders</h1>
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <Package className="h-16 w-16 text-muted-foreground mb-4" />
          <p className="text-lg font-medium text-muted-foreground">No orders yet</p>
          <p className="text-sm text-muted-foreground mt-1">
            When you place an order, it will appear here.
          </p>
          <Link to="/products" className="mt-4">
            <Button variant="outline">Browse Products</Button>
          </Link>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold">My Orders</h1>

      <div className="space-y-4">
        {orders.map((order) => (
          <Card key={order.id}>
            <CardContent className="p-6">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div className="space-y-1">
                  <div className="flex items-center gap-3">
                    <h2 className="text-lg font-semibold">Order #{order.id}</h2>
                    <OrderStatusBadge status={order.status} size="sm" />
                  </div>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(order.createdAt)} · {order.items.length} item{order.items.length !== 1 ? 's' : ''}
                  </p>
                  <p className="text-sm text-muted-foreground line-clamp-1">
                    {order.shippingFullName} — {order.shippingCity}
                  </p>
                </div>

                <div className="flex items-center gap-4">
                  <div className="text-right">
                    <p className="text-lg font-bold">{formatPrice(order.total)}</p>
                  </div>
                  <Link to={`/orders/${order.id}`}>
                    <Button variant="outline" size="sm">
                      View Details
                      <ArrowRight className="ml-1 h-4 w-4" />
                    </Button>
                  </Link>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {totalPages > 1 && (
        <Pagination currentPage={page} totalPages={totalPages} onPageChange={goToPage} />
      )}
    </div>
  )
}
