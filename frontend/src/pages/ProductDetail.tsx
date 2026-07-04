import { useParams, Link } from 'react-router-dom'
import { useProduct } from '@/lib/useProducts'
import { Button } from '@/components/ui/button'
import { ArrowLeft } from 'lucide-react'

function formatPrice(price: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(price)
}

export function ProductDetail() {
  const { id } = useParams<{ id: string }>()
  const productId = Number(id)

  const { data, isLoading, isError } = useProduct(productId)
  const product = data?.data

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
        </div>
      </div>
    </div>
  )
}
