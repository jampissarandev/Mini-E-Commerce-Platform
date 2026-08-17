import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Package } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'
import { OrderStatusSelect } from '@/components/admin/OrderStatusSelect'
import { useAdminOrder } from '@/lib/useAdminOrders'

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

/**
 * Mock payment status badge (16b). v1 has no payment transaction id — the
 * `Order` entity has no payment columns and `MockPaymentService` does not
 * persist a record, so we derive a mock status from `Order.Status`:
 * Paid → "Captured (mock)", anything else → "Not captured (mock)".
 */
function PaymentStatusBadge({ status }: { status: string }) {
  const captured = status === 'Paid'
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
        captured ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'
      }`}
    >
      {captured ? 'Captured (mock)' : 'Not captured (mock)'}
    </span>
  )
}

/**
 * Admin order detail (Task 16b): customer, items with live thumbnails,
 * shipping address, mock payment status, totals, and the status-update
 * dropdown (16c). Basic `@media print` styles hide the nav/actions.
 */
export function AdminOrderDetail() {
  const { id } = useParams()
  const orderId = Number(id)
  const { data, isLoading, isError } = useAdminOrder(orderId)

  if (!Number.isFinite(orderId) || orderId <= 0) {
    return <NotFoundState />
  }

  if (isLoading) {
    return (
      <div
        className="flex items-center justify-center py-12"
        role="status"
        aria-label="Loading order"
      >
        <p className="text-muted-foreground">Loading order...</p>
      </div>
    )
  }

  if (isError || !data?.data) {
    return <NotFoundState />
  }

  const order = data.data

  return (
    <div className="space-y-6 print:space-y-4">
      {/* Back + status controls (hidden when printing) */}
      <div className="flex items-center justify-between print:hidden">
        <Link to="/admin/orders">
          <Button variant="ghost" size="icon-sm" aria-label="Back to orders">
            <ArrowLeft className="h-4 w-4" />
          </Button>
        </Link>
        <OrderStatusSelect order={order} />
      </div>

      {/* Title row */}
      <div className="flex items-center gap-3">
        <h1 className="text-3xl font-bold">Order #{order.id}</h1>
        <OrderStatusBadge status={order.status} />
      </div>

      {/* Print-only meta line */}
      <div className="hidden print:block">
        <p className="text-sm text-muted-foreground">
          {formatDate(order.createdAt)}
        </p>
      </div>

      {/* Customer + Payment */}
      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Customer</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p className="font-medium">{order.customer.fullName}</p>
            <p className="text-muted-foreground">{order.customer.email}</p>
            <p className="text-xs text-muted-foreground">
              Placed {formatDate(order.createdAt)}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Payment</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <div className="flex items-center gap-2">
              <span className="text-muted-foreground">Status:</span>
              <PaymentStatusBadge status={order.status} />
            </div>
            <p className="text-xs text-muted-foreground">
              Mock payment provider — no transaction id is stored in v1.
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Items + totals */}
      <Card>
        <CardHeader>
          <CardTitle>Items</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left">
                  <th className="px-4 py-3 font-medium">Product</th>
                  <th className="px-4 py-3 font-medium text-right">
                    Unit Price
                  </th>
                  <th className="px-4 py-3 font-medium text-right">Qty</th>
                  <th className="px-4 py-3 font-medium text-right">Subtotal</th>
                </tr>
              </thead>
              <tbody>
                {order.items.map((item) => (
                  <tr key={item.id} className="border-b last:border-b-0">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        {item.imageUrl && (
                          <img
                            src={item.imageUrl}
                            alt={item.productName}
                            className="h-10 w-10 rounded-md object-cover"
                          />
                        )}
                        <span className="font-medium">{item.productName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {formatPrice(item.unitPrice)}
                    </td>
                    <td className="px-4 py-3 text-right">{item.quantity}</td>
                    <td className="px-4 py-3 text-right">
                      {formatPrice(item.subtotal)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 space-y-1 border-t pt-4 text-sm">
            <div className="flex justify-between">
              <span className="text-muted-foreground">Subtotal</span>
              <span>{formatPrice(order.subtotal)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted-foreground">Shipping</span>
              <span>{formatPrice(order.shippingFee)}</span>
            </div>
            <div className="flex justify-between text-base font-bold">
              <span>Total</span>
              <span>{formatPrice(order.total)}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Shipping address */}
      <Card>
        <CardHeader>
          <CardTitle>Shipping Address</CardTitle>
        </CardHeader>
        <CardContent className="text-sm">
          <p className="font-medium">{order.shippingFullName}</p>
          <p>{order.shippingStreet}</p>
          <p>
            {order.shippingCity} {order.shippingPostalCode}
          </p>
          <p>{order.shippingCountry}</p>
          <p className="text-muted-foreground">{order.shippingPhone}</p>
        </CardContent>
      </Card>
    </div>
  )
}

function NotFoundState() {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <Package className="mb-4 h-16 w-16 text-muted-foreground" />
      <p className="text-lg font-medium text-muted-foreground">
        Order not found
      </p>
      <Link to="/admin/orders" className="mt-4">
        <Button variant="outline">Back to Orders</Button>
      </Link>
    </div>
  )
}