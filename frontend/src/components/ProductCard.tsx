import { Link } from 'react-router-dom'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { ShoppingCart } from 'lucide-react'
import { useAddToCart } from '@/lib/useCart'
import type { ProductListItem } from '@/lib/types'

interface ProductCardProps {
  product: ProductListItem
}

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

export function ProductCard({ product }: ProductCardProps) {
  const addToCart = useAddToCart()

  function handleAddToCart(e: React.MouseEvent) {
    e.preventDefault() // Prevent navigating to product detail
    addToCart.mutate({ productId: product.id, quantity: 1 })
  }

  return (
    <Link to={`/products/${product.id}`} className="group block">
      <Card className="overflow-hidden transition-shadow hover:shadow-md h-full">
        <div className="aspect-square overflow-hidden bg-muted">
          {product.imageUrl ? (
            <img
              src={product.imageUrl}
              alt={product.name}
              className="h-full w-full object-cover transition-transform group-hover:scale-105"
            />
          ) : (
            <div className="flex h-full items-center justify-center text-muted-foreground text-sm">
              No Image
            </div>
          )}
        </div>
        <CardContent className="p-4 space-y-3">
          <div className="space-y-1">
            <p className="text-xs text-muted-foreground">{product.categoryName}</p>
            <h3 className="font-semibold text-sm leading-tight line-clamp-2">{product.name}</h3>
            <p className="text-lg font-bold">{formatPrice(product.price)}</p>
          </div>
          <Button
            variant="outline"
            size="sm"
            className="w-full"
            onClick={handleAddToCart}
            disabled={addToCart.isPending}
          >
            <ShoppingCart className="mr-2 h-4 w-4" />
            {addToCart.isPending ? 'Adding...' : 'Add to Cart'}
          </Button>
        </CardContent>
      </Card>
    </Link>
  )
}
