import { Link, useParams } from 'react-router-dom'
import { CheckCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'
import { useOrder } from '@/lib/useOrders'

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
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function OrderConfirmation() {
  const { id } = useParams<{ id: string }>()
  const orderId = Number(id)
  const { data, isLoading, error } = useOrder(orderId)

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading order">
        <p className="text-muted-foreground">Loading order details...</p>
      </div>
    )
  }

  if (error || !data?.success || !data?.data) {
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold">Order Not Found</h1>
        <p className="text-muted-foreground">
          {error ? 'Failed to load order details.' : 'Order not found.'}
        </p>
        <Link to="/products">
          <Button variant="outline">Continue Shopping</Button>
        </Link>
      </div>
    )
  }

  const order = data.data

  return (
    <div className="space-y-6 max-w-3xl mx-auto">
      {/* Header */}
      <div className="text-center space-y-2">
        <CheckCircle className="h-12 w-12 text-green-600 mx-auto" />
        <h1 className="text-3xl font-bold">Order #{order.id}</h1>
        <div className="flex justify-center">
          <OrderStatusBadge status={order.status} />
        </div>
      </div>

      {/* Order details */}
      <Card>
        <CardHeader>
          <CardTitle>Order Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Order Date</span>
            <span>{formatDate(order.createdAt)}</span>
          </div>
          <div className="text-sm space-y-1">
            <span className="text-muted-foreground block">Shipping Address</span>
            <div className="text-right">
              <p className="font-medium">{order.shippingFullName}</p>
              <p>{order.shippingStreet}</p>
              <p>
                {order.shippingCity}, {order.shippingPostalCode}
              </p>
              <p>{order.shippingCountry}</p>
              <p className="text-muted-foreground">{order.shippingPhone}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Items */}
      <Card>
        <CardHeader>
          <CardTitle>Items</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {order.items.map((item) => (
            <div key={item.id} className="flex items-center justify-between py-2 border-b last:border-0">
              <div>
                <p className="font-medium">{item.productName}</p>
                <p className="text-sm text-muted-foreground">
                  {formatPrice(item.unitPrice)} × {item.quantity}
                </p>
              </div>
              <span className="font-medium">{formatPrice(item.subtotal)}</span>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Totals */}
      <Card>
        <CardContent className="p-6 space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Subtotal</span>
            <span>{formatPrice(order.subtotal)}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Shipping</span>
            <span>{formatPrice(order.shippingFee)}</span>
          </div>
          <div className="flex justify-between text-lg font-semibold border-t pt-2">
            <span>Total</span>
            <span>{formatPrice(order.total)}</span>
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex flex-col sm:flex-row justify-center gap-3">
        <Link to="/orders">
          <Button variant="outline" size="lg">View My Orders</Button>
        </Link>
        <Link to="/products">
          <Button variant="outline" size="lg">Continue Shopping</Button>
        </Link>
      </div>
    </div>
  )
}
