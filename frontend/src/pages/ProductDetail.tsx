import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useProduct } from '@/lib/useProducts'
import { useAddToCart } from '@/lib/useCart'
import { Button } from '@/components/ui/button'
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

  const { data, isLoading, isError } = useProduct(productId)
  const product = data?.data
  const addToCart = useAddToCart()

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

          <div className="space-y-1">
            <p className="text-sm text-muted-foreground">
              Stock: <span className="font-medium text-foreground">{product.stock} in stock</span>
            </p>
          </div>

          {/* Add to Cart */}
          {product.stock > 0 && (
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
                    onClick={() => setQuantity((q) => Math.min(product.stock, q + 1))}
                    disabled={quantity >= product.stock}
                  >
                    <Plus className="h-3 w-3" />
                  </Button>
                </div>
              </div>
              <Button
                size="lg"
                className="w-full"
                onClick={() => {
                  addToCart.mutate(
                    { productId: product.id, quantity },
                    {
                      onSuccess: () => {
                        setJustAdded(true)
                        setTimeout(() => setJustAdded(false), 2000)
                      },
                    },
                  )
                }}
                disabled={addToCart.isPending}
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
