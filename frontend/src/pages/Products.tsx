import { useSearchParams } from 'react-router-dom'
import { useProducts, useCategories } from '@/lib/useProducts'
import { ProductGrid } from '@/components/ProductGrid'
import { Pagination } from '@/components/Pagination'
import { SearchBar } from '@/components/SearchBar'
import { CategoryFilter } from '@/components/CategoryFilter'

export function Products() {
  const [searchParams, setSearchParams] = useSearchParams()

  const page = Number(searchParams.get('page') || '1')
  const search = searchParams.get('search') || ''
  const category = searchParams.get('category') || ''
  const sort = searchParams.get('sort') || ''

  const { data: productsData, isLoading, isError } = useProducts({
    page,
    search: search || undefined,
    category: category || undefined,
    sort: sort || undefined,
  })

  const { data: categoriesData } = useCategories()

  function updateParams(updates: Record<string, string>) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      Object.entries(updates).forEach(([key, value]) => {
        if (value) {
          next.set(key, value)
        } else {
          next.delete(key)
        }
      })
      // Reset to page 1 when filters change (unless we're changing the page itself)
      if (!('page' in updates)) {
        next.delete('page')
      }
      return next
    })
  }

  return (
    <div className="space-y-6">
      <h1 className="text-3xl font-bold">Products</h1>

      {/* Search & Filters */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
        <SearchBar
          defaultValue={search}
          onSearch={(value) => updateParams({ search: value })}
        />
        <select
          value={sort}
          onChange={(e) => updateParams({ sort: e.target.value })}
          className="h-9 rounded-md border bg-transparent px-3 text-sm"
          aria-label="Sort products"
        >
          <option value="">Sort by: Name</option>
          <option value="price_asc">Price: Low to High</option>
          <option value="price_desc">Price: High to Low</option>
          <option value="newest">Newest</option>
          <option value="name_asc">Name: A-Z</option>
          <option value="name_desc">Name: Z-A</option>
        </select>
      </div>

      {categoriesData?.data && (
        <CategoryFilter
          categories={categoriesData.data}
          selectedCategory={category || undefined}
          onCategoryChange={(slug) => updateParams({ category: slug || '' })}
        />
      )}

      {/* Content */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12" role="status" aria-label="Loading products">
          <p className="text-muted-foreground">Loading products...</p>
        </div>
      ) : isError ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <p className="text-lg font-medium text-destructive">Something went wrong</p>
          <p className="text-sm text-muted-foreground">Please try again later.</p>
        </div>
      ) : (
        <>
          <ProductGrid products={productsData?.data || []} />
          {productsData?.meta && (
            <Pagination
              currentPage={productsData.meta.page}
              totalPages={productsData.meta.totalPages}
              onPageChange={(p) => updateParams({ page: String(p) })}
            />
          )}
        </>
      )}
    </div>
  )
}
