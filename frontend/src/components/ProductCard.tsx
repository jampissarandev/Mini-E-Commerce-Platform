import { Link } from 'react-router-dom'
import { Card, CardContent } from '@/components/ui/card'
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
        <CardContent className="p-4">
          <div className="space-y-1">
            <p className="text-xs text-muted-foreground">{product.categoryName}</p>
            <h3 className="font-semibold text-sm leading-tight line-clamp-2">{product.name}</h3>
            <p className="text-lg font-bold">{formatPrice(product.price)}</p>
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
