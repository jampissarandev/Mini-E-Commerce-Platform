import { Link, useNavigate } from 'react-router-dom'
import { Card, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import type { ProductListItem } from '@/lib/types'
import { formatCurrency } from '@/lib/utils'
import { useAddToCart } from '@/lib/useCart'
import { pickPreferredVariant } from '@/lib/variant-utils'

interface ProductCardProps {
  product: ProductListItem
}

export function ProductCard({ product }: ProductCardProps) {
  const navigate = useNavigate()
  const addToCart = useAddToCart()

  // Quick-add (ADR 0008 Phase C): add the preferred variant when it has
  // stock; navigate to the detail page when every variant is OOS (Q4 —
  // never disable the button) or the summary is missing (stale cache).
  function handleQuickAdd() {
    const preferred = pickPreferredVariant(product.variants ?? [])
    if (!preferred || preferred.stock === 0) {
      navigate(`/products/${product.id}`)
      return
    }
    addToCart.mutate({ productVariantId: preferred.id, quantity: 1 })
  }

  return (
    <Card className="overflow-hidden transition-shadow hover:shadow-md h-full flex flex-col">
      <Link to={`/products/${product.id}`} className="group block flex-1">
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
            <p className="text-lg font-bold">{formatCurrency(product.price)}</p>
            {product.variantCount > 1 && (
              <p className="text-xs text-muted-foreground" aria-label={`${product.variantCount} variants available`}>
                {product.variantCount} variants
              </p>
            )}
          </div>
        </CardContent>
      </Link>
      {/* Single quick-add button outside the card link (Q8): nested
          interactive elements inside the <Link> would be an a11y violation. */}
      <div className="px-4 pb-4 mt-auto">
        <Button variant="outline" size="sm" className="w-full" onClick={handleQuickAdd}>
          Add to cart
        </Button>
      </div>
    </Card>
  )
}
