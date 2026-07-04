import { Link } from 'react-router-dom'
import { Trash2, Plus, Minus, ShoppingBag } from 'lucide-react'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetFooter } from '@/components/ui/sheet'
import { Button } from '@/components/ui/button'
import { useCart, useUpdateCartItem, useRemoveCartItem, useClearCart } from '@/lib/useCart'

interface CartSheetProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

export function CartSheet({ open, onOpenChange }: CartSheetProps) {
  const { data: cartData, isLoading } = useCart()
  const updateItem = useUpdateCartItem()
  const removeItem = useRemoveCartItem()
  const clearCart = useClearCart()

  const items = cartData?.data.items ?? []
  const total = cartData?.data.total ?? 0

  function handleQuantityChange(id: number, newQuantity: number) {
    if (newQuantity < 1) {
      removeItem.mutate(id)
    } else {
      updateItem.mutate({ id, quantity: newQuantity })
    }
  }

  function handleRemoveItem(id: number) {
    removeItem.mutate(id)
  }

  function handleClearCart() {
    clearCart.mutate()
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="flex flex-col w-full sm:max-w-sm">
        <SheetHeader>
          <SheetTitle>Shopping Cart</SheetTitle>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-4">
          {isLoading ? (
            <p className="text-sm text-muted-foreground py-8 text-center">Loading cart...</p>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <ShoppingBag className="h-12 w-12 text-muted-foreground mb-4" />
              <p className="text-lg font-medium text-muted-foreground">Your cart is empty</p>
              <Link
                to="/products"
                className="mt-4 text-sm text-primary underline-offset-4 hover:underline"
                onClick={() => onOpenChange(false)}
              >
                Browse products
              </Link>
            </div>
          ) : (
            <div className="space-y-4">
              {items.map((item) => (
                <div
                  key={item.id}
                  className="flex gap-3 rounded-lg border p-3"
                >
                  <div className="h-16 w-16 shrink-0 overflow-hidden rounded-md bg-muted">
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
                        className="text-sm font-medium leading-tight truncate block hover:underline"
                        onClick={() => onOpenChange(false)}
                      >
                        {item.productName}
                      </Link>
                      <p className="text-sm text-muted-foreground">
                        {formatPrice(item.unitPrice)}
                      </p>
                    </div>
                    <div className="flex items-center justify-between mt-1">
                      <div className="flex items-center gap-1">
                        <Button
                          variant="outline"
                          size="icon-xs"
                          aria-label="Decrease quantity"
                          onClick={() => handleQuantityChange(item.id, item.quantity - 1)}
                          disabled={updateItem.isPending}
                        >
                          <Minus className="h-3 w-3" />
                        </Button>
                        <span className="w-8 text-center text-sm font-medium">
                          {item.quantity}
                        </span>
                        <Button
                          variant="outline"
                          size="icon-xs"
                          aria-label="Increase quantity"
                          onClick={() => handleQuantityChange(item.id, item.quantity + 1)}
                          disabled={updateItem.isPending}
                        >
                          <Plus className="h-3 w-3" />
                        </Button>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium">
                          {formatPrice(item.subtotal)}
                        </span>
                        <Button
                          variant="ghost"
                          size="icon-xs"
                          aria-label="Remove item"
                          onClick={() => handleRemoveItem(item.id)}
                          disabled={removeItem.isPending}
                        >
                          <Trash2 className="h-3 w-3" />
                        </Button>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {items.length > 0 && (
          <SheetFooter>
            <div className="flex items-center justify-between mb-2 px-0">
              <span className="text-base font-semibold">Total</span>
              <span className="text-base font-semibold">{formatPrice(total)}</span>
            </div>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={handleClearCart}
                disabled={clearCart.isPending}
              >
                Clear
              </Button>
              <Link to="/checkout" className="flex-1" onClick={() => onOpenChange(false)}>
                <Button className="w-full" size="sm">
                  Checkout
                </Button>
              </Link>
            </div>
          </SheetFooter>
        )}
      </SheetContent>
    </Sheet>
  )
}
