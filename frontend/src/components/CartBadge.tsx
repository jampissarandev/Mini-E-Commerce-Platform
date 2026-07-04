import { useState } from 'react'
import { ShoppingCart } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useCartItemCount } from '@/lib/useCart'
import { CartSheet } from '@/components/CartSheet'

export function CartBadge() {
  const [open, setOpen] = useState(false)
  const itemCount = useCartItemCount()

  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        aria-label="Open cart"
        onClick={() => setOpen(true)}
        className="relative"
      >
        <ShoppingCart className="h-5 w-5" />
        {itemCount > 0 && (
          <span
            role="status"
            className="absolute -top-1 -right-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1 text-xs font-medium text-primary-foreground"
          >
            {itemCount > 9 ? '9+' : itemCount}
          </span>
        )}
      </Button>
      <CartSheet open={open} onOpenChange={setOpen} />
    </>
  )
}
