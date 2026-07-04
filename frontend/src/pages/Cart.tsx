import { Link } from 'react-router-dom'
import { Trash2, Plus, Minus, ShoppingBag } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { useCart, useUpdateCartItem, useRemoveCartItem, useClearCart } from '@/lib/useCart'

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

export function Cart() {
  const { data: cartData, isLoading } = useCart()
  const updateItem = useUpdateCartItem()
  const removeItem = useRemoveCartItem()
  const clearCart = useClearCart()

  const items = cartData?.data.items ?? []
  const total = cartData?.data.total ?? 0

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading cart">
        <p className="text-muted-foreground">Loading cart...</p>
      </div>
    )
  }

  if (items.length === 0) {
    return (
      <div className="space-y-6">
        <h1 className="text-3xl font-bold">Shopping Cart</h1>
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <ShoppingBag className="h-16 w-16 text-muted-foreground mb-4" />
          <p className="text-lg font-medium text-muted-foreground">Your cart is empty</p>
          <Link to="/products" className="mt-4">
            <Button variant="outline">Browse Products</Button>
          </Link>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Shopping Cart</h1>
        <Button
          variant="outline"
          size="sm"
          onClick={() => clearCart.mutate()}
          disabled={clearCart.isPending}
        >
          Clear Cart
        </Button>
      </div>

      <div className="space-y-4">
        {items.map((item) => (
          <Card key={item.id}>
            <CardContent className="flex gap-4 p-4">
              <div className="h-20 w-20 shrink-0 overflow-hidden rounded-md bg-muted">
                {item.imageUrl ? (
                  <img
                    src={item.imageUrl}
                    alt={item.productName}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <div className="flex h-full items-center justify-center text-xs text-muted-foreground">
                    No image
                  </div>
                )}
              </div>
              <div className="flex flex-1 flex-col justify-between min-w-0">
                <div className="min-w-0">
                  <Link
                    to={`/products/${item.productId}`}
                    className="text-sm font-semibold leading-tight hover:underline"
                  >
                    {item.productName}
                  </Link>
                  <p className="text-sm text-muted-foreground mt-0.5">
                    {formatPrice(item.unitPrice)} each
                  </p>
                </div>
                <div className="flex items-center justify-between mt-2">
                  <div className="flex items-center gap-1">
                    <Button
                      variant="outline"
                      size="icon-xs"
                      aria-label="Decrease quantity"
                      onClick={() => {
                        if (item.quantity <= 1) {
                          removeItem.mutate(item.id)
                        } else {
                          updateItem.mutate({ id: item.id, quantity: item.quantity - 1 })
                        }
                      }}
                      disabled={updateItem.isPending || removeItem.isPending}
                    >
                      <Minus className="h-3 w-3" />
                    </Button>
                    <span className="w-10 text-center text-sm font-medium">{item.quantity}</span>
                    <Button
                      variant="outline"
                      size="icon-xs"
                      aria-label="Increase quantity"
                      onClick={() => updateItem.mutate({ id: item.id, quantity: item.quantity + 1 })}
                      disabled={updateItem.isPending}
                    >
                      <Plus className="h-3 w-3" />
                    </Button>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-sm font-semibold">{formatPrice(item.subtotal)}</span>
                    <Button
                      variant="ghost"
                      size="icon-xs"
                      aria-label="Remove item"
                      onClick={() => removeItem.mutate(item.id)}
                      disabled={removeItem.isPending}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Summary */}
      <Card>
        <CardContent className="p-6 space-y-4">
          <div className="flex items-center justify-between">
            <span className="text-lg font-semibold">Total</span>
            <span className="text-lg font-bold">{formatPrice(total)}</span>
          </div>
          <Link to="/checkout">
            <Button className="w-full" size="lg">
              Proceed to Checkout
            </Button>
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
