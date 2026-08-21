import { useEffect, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useProduct } from '@/lib/useProducts'
import { useAddToCart } from '@/lib/useCart'
import { Button } from '@/components/ui/button'
import { VariantPicker } from '@/components/VariantPicker'
import { ArrowLeft, ShoppingCart, Plus, Minus, Check } from 'lucide-react'

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

export function ProductDetail() {
  const { id } = useParams<{ id: string }>()
  const productId = Number(id)
  const [quantity, setQuantity] = useState(1)
  const [justAdded, setJustAdded] = useState(false)
  const [selectedVariantId, setSelectedVariantId] = useState<number | null>(null)

  const { data, isLoading, isError } = useProduct(productId)
  const product = data?.data
  const addToCart = useAddToCart()

  // Auto-select the first in-stock variant when product loads / changes.
  // UseEffect must be placed after the isLoading/isError early returns are
  // handled via a data memo, so we compute the preferred variant synchronously
  // and fall back to an effect for subsequent product switches.
  const preferredVariantId = (() => {
    const variants = product?.variants ?? []
    if (variants.length === 0) return null
    const active = variants.filter((v) => v.isActive)
    return (active.find((v) => v.stock > 0) ?? active[0] ?? null)?.id ?? null
  })()

  useEffect(() => {
    if (preferredVariantId !== null) {
      setSelectedVariantId((prev) => (prev === null ? preferredVariantId : prev))
    }
  }, [preferredVariantId])

  // Keep quantity in sync when product changes
  useEffect(() => {
    if (product) setQuantity(1)
  }, [product?.id])

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12" role="status" aria-label="Loading product">
        <p className="text-muted-foreground">Loading product...</p>
      </div>
    )
  }

  if (isError || !product) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <h2 className="text-2xl font-bold">Product Not Found</h2>
        <p className="mt-2 text-muted-foreground">The product you're looking for doesn't exist.</p>
        <Link to="/products" className="mt-4">
          <Button variant="outline">
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to Products
          </Button>
        </Link>
      </div>
    )
  }

  const mainImage = product.images[0]

  return (
    <div className="space-y-6">
      <Link to="/products" className="inline-flex items-center text-sm text-muted-foreground hover:text-foreground transition-colors">
        <ArrowLeft className="mr-1 h-4 w-4" />
        Back to Products
      </Link>

      <div className="grid gap-8 md:grid-cols-2">
        {/* Images */}
        <div className="space-y-4">
          {mainImage && (
            <div className="aspect-square overflow-hidden rounded-lg border bg-muted">
              <img
                src={mainImage.url}
                alt={product.name}
                className="h-full w-full object-cover"
              />
            </div>
          )}
          {product.images.length > 1 && (
            <div className="grid grid-cols-4 gap-2">
              {product.images.map((image) => (
                <div key={image.id} className="aspect-square overflow-hidden rounded-md border bg-muted">
                  <img
                    src={image.url}
                    alt={`${product.name} ${image.sortOrder + 1}`}
                    className="h-full w-full object-cover"
                  />
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Details */}
        <div className="space-y-6">
          <div>
            <p className="text-sm text-muted-foreground">{product.category.name}</p>
            <h1 className="text-3xl font-bold">{product.name}</h1>
          </div>

          <p className="text-3xl font-bold">{formatPrice(product.price)}</p>

          <div className="space-y-2">
            <h2 className="text-lg font-semibold">Description</h2>
            <p className="text-muted-foreground leading-relaxed">{product.description}</p>
          </div>

          {/* Variant picker — ADR 0003: stock lives on the variant */}
          {product.variants.length > 0 && (
            <VariantPicker
              variants={product.variants}
              selectedId={selectedVariantId}
              onSelect={(id) => {
                setSelectedVariantId(id)
                setQuantity(1)
              }}
            />
          )}

          {/* Stock for selected variant — also render synchronously on first paint
              by falling back to preferredVariantId when selectedVariantId hasn't settled yet */}
          {(() => {
            const effectiveId = selectedVariantId ?? preferredVariantId
            const selected = product.variants.find((v) => v.id === effectiveId)
            const stock = selected?.stock ?? 0
            const outOfStock = product.variants.every((v) => !v.isActive || v.stock === 0)
            if (outOfStock) {
              return <p className="text-sm font-medium text-destructive">Out of stock</p>
            }
            if (!selected) return null
            return (
              <p className="text-sm text-muted-foreground">
                Stock: <span className="font-medium text-foreground">{stock} in stock</span>
              </p>
            )
          })()}

          {/* Add to Cart — requires a selected in-stock variant */}
          {(() => {
            const selected = product.variants.find((v) => v.id === selectedVariantId)
            const stock = selected?.stock ?? 0
            const canAdd = selected !== undefined && stock > 0
            if (!canAdd) return null
            return (
              <div className="space-y-4">
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium">Quantity:</span>
                  <div className="flex items-center gap-1">
                    <Button
                      variant="outline"
                      size="icon-xs"
                      aria-label="Decrease quantity"
                      onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                      disabled={quantity <= 1}
                    >
                      <Minus className="h-3 w-3" />
                    </Button>
                    <span className="w-10 text-center text-sm font-medium">{quantity}</span>
                    <Button
                      variant="outline"
                      size="icon-xs"
                      aria-label="Increase quantity"
                      onClick={() => setQuantity((q) => Math.min(stock, q + 1))}
                      disabled={quantity >= stock}
                    >
                      <Plus className="h-3 w-3" />
                    </Button>
                  </div>
                </div>
                <Button
                  size="lg"
                  className="w-full"
                  onClick={() => {
                    if (selectedVariantId == null) return
                    addToCart.mutate(
                      { productVariantId: selectedVariantId, quantity },
                      {
                        onSuccess: () => {
                          setJustAdded(true)
                          setTimeout(() => setJustAdded(false), 2000)
                        },
                      },
                    )
                  }}
                  disabled={addToCart.isPending || selectedVariantId == null}
                >
                  {justAdded ? (
                    <>
                      <Check className="mr-2 h-4 w-4" />
                      Added to Cart!
                    </>
                  ) : (
                    <>
                      <ShoppingCart className="mr-2 h-4 w-4" />
                      {addToCart.isPending ? 'Adding...' : 'Add to Cart'}
                    </>
                  )}
                </Button>
              </div>
            )
          })()}
        </div>
      </div>
    </div>
  )
}
