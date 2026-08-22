import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useProduct } from '@/lib/useProducts'
import { useAddToCart } from '@/lib/useCart'
import { Button } from '@/components/ui/button'
import { VariantPicker } from '@/components/VariantPicker'
import { ArrowLeft, ShoppingCart, Plus, Minus, Check } from 'lucide-react'
import { formatCurrency } from '@/lib/utils'
import { pickPreferredVariant } from '@/lib/variant-utils'

export function ProductDetail() {
  const { id } = useParams<{ id: string }>()
  const productId = Number(id)
  const [quantity, setQuantity] = useState(1)
  const [justAdded, setJustAdded] = useState(false)
  const [selectedVariantId, setSelectedVariantId] = useState<number | null>(null)
  // Track the last product we initialised selection for so we can reset on
  // navigation to a different product without using useEffect (which would
  // trigger the react-hooks/set-state-in-effect lint rule).
  const [prevProductId, setPrevProductId] = useState<number | null>(null)

  const { data, isLoading, isError } = useProduct(productId)
  const product = data?.data
  const addToCart = useAddToCart()

  // Derive the preferred variant synchronously on every render so it's
  // available on first paint of the Stock line. The picker is also reset
  // when navigating to a different product (see below).
  const preferredVariantId = pickPreferredVariant(product?.variants ?? [])?.id ?? null

  // Reset variant selection and quantity when navigating to a different
  // product. This is the React-blessed "set state during render" pattern for
  // resetting state when a prop changes
  // (https://react.dev/learn/you-might-not-need-an-effect#resetting-all-state-when-a-prop-changes).
  // Must run before any early returns below so it applies on every render.
  if (product?.id != null && product.id !== prevProductId) {
    setPrevProductId(product.id)
    setSelectedVariantId(preferredVariantId)
    setQuantity(1)
  }

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
  // The Stock line falls back to the preferred variant until the user picks
  // one explicitly; the Add-to-Cart block requires an explicit selection.
  const effectiveVariantId = selectedVariantId ?? preferredVariantId
  const selectedVariant =
    effectiveVariantId == null
      ? undefined
      : product.variants.find((v) => v.id === effectiveVariantId)
  const outOfStock = product.variants.every((v) => !v.isActive || v.stock === 0)
  const canAdd = selectedVariantId != null && selectedVariant !== undefined && selectedVariant.stock > 0

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

          <p className="text-3xl font-bold">{formatCurrency(product.price)}</p>

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
          {outOfStock ? (
            <p className="text-sm font-medium text-destructive">Out of stock</p>
          ) : selectedVariant ? (
            <p className="text-sm text-muted-foreground">
              Stock: <span className="font-medium text-foreground">{selectedVariant.stock} in stock</span>
            </p>
          ) : null}

          {/* Add to Cart — requires a selected in-stock variant */}
          {canAdd && (
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
                    onClick={() => setQuantity((q) => Math.min(selectedVariant.stock, q + 1))}
                    disabled={quantity >= selectedVariant.stock}
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
          )}
        </div>
      </div>
    </div>
  )
}
