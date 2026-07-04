import { useState, useCallback } from 'react'
import { Button } from '@/components/ui/button'
import { Pagination } from '@/components/Pagination'
import { AdminProductTable } from '@/components/AdminProductTable'
import { AdminProductForm } from '@/components/AdminProductForm'
import { DeleteConfirmDialog } from '@/components/DeleteConfirmDialog'
import { Plus } from 'lucide-react'
import {
  useAdminProducts,
  useCreateProduct,
  useUpdateProduct,
  useDeleteProduct,
} from '@/lib/useAdminProducts'
import { useCategories } from '@/lib/useProducts'
import type { AdminProductListItem } from '@/lib/types'
import type { AdminProductValues } from '@/lib/schemas/adminProduct'

export function AdminProducts() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [editingProduct, setEditingProduct] =
    useState<AdminProductListItem | null>(null)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [deletingProduct, setDeletingProduct] =
    useState<AdminProductListItem | null>(null)

  const { data: productsData, isLoading } = useAdminProducts({
    page,
    pageSize: 10,
    q: search || undefined,
  })

  const { data: categoriesData } = useCategories()
  const createProduct = useCreateProduct()
  const updateProduct = useUpdateProduct()
  const deleteProduct = useDeleteProduct()

  const categories = categoriesData?.data ?? []
  const products = productsData?.data ?? []
  const meta = productsData?.meta

  const handleAdd = useCallback(() => {
    setEditingProduct(null)
    setFormOpen(true)
  }, [])

  const handleEdit = useCallback((product: AdminProductListItem) => {
    setEditingProduct(product)
    setFormOpen(true)
  }, [])

  const handleDelete = useCallback((product: AdminProductListItem) => {
    setDeletingProduct(product)
    setDeleteDialogOpen(true)
  }, [])

  const handleFormSubmit = useCallback(
    (values: AdminProductValues) => {
      if (editingProduct) {
        updateProduct.mutate(
          { id: editingProduct.id, request: values },
          {
            onSuccess: () => {
              setFormOpen(false)
              setEditingProduct(null)
            },
          },
        )
      } else {
        createProduct.mutate(values, {
          onSuccess: () => {
            setFormOpen(false)
          },
        })
      }
    },
    [editingProduct, createProduct, updateProduct],
  )

  const handleConfirmDelete = useCallback(() => {
    if (!deletingProduct) return
    deleteProduct.mutate(deletingProduct.id, {
      onSuccess: () => {
        setDeleteDialogOpen(false)
        setDeletingProduct(null)
      },
    })
  }, [deletingProduct, deleteProduct])

  const isSubmitting = createProduct.isPending || updateProduct.isPending

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Products Management</h1>
        <Button onClick={handleAdd}>
          <Plus className="mr-2 h-4 w-4" />
          Add Product
        </Button>
      </div>

      {/* Search */}
      <div>
        <input
          type="search"
          placeholder="Search products..."
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
          className="h-9 w-full max-w-sm rounded-md border bg-transparent px-3 text-sm"
          aria-label="Search products"
        />
      </div>

      {/* Table */}
      {isLoading ? (
        <div
          className="flex items-center justify-center py-12"
          role="status"
          aria-label="Loading products"
        >
          <p className="text-muted-foreground">Loading products...</p>
        </div>
      ) : products.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12">
          <p className="text-lg text-muted-foreground">No products found</p>
        </div>
      ) : (
        <>
          <AdminProductTable
            products={products}
            onEdit={handleEdit}
            onDelete={handleDelete}
          />

          {meta && meta.totalPages > 1 && (
            <Pagination
              currentPage={meta.page}
              totalPages={meta.totalPages}
              onPageChange={setPage}
            />
          )}
        </>
      )}

      {/* Add/Edit Form */}
      <AdminProductForm
        open={formOpen}
        onOpenChange={setFormOpen}
        product={editingProduct}
        categories={categories}
        onSubmit={handleFormSubmit}
        isSubmitting={isSubmitting}
      />

      {/* Delete Confirmation */}
      <DeleteConfirmDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        product={deletingProduct}
        onConfirm={handleConfirmDelete}
        isDeleting={deleteProduct.isPending}
      />
    </div>
  )
}
