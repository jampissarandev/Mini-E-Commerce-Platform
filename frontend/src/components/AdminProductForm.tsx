import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from '@/components/ui/sheet'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  adminProductSchema,
  type AdminProductValues,
} from '@/lib/schemas/adminProduct'
import type { AdminProductListItem, CategoryDto } from '@/lib/types'

interface AdminProductFormProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  product?: AdminProductListItem | null
  categories: CategoryDto[]
  onSubmit: (values: AdminProductValues) => void
  isSubmitting: boolean
}

export function AdminProductForm({
  open,
  onOpenChange,
  product,
  categories,
  onSubmit,
  isSubmitting,
}: AdminProductFormProps) {
  const isEditing = !!product

  const form = useForm<AdminProductValues>({
    resolver: zodResolver(adminProductSchema),
    defaultValues: {
      name: '',
      slug: '',
      description: '',
      price: 0,
      stock: 0,
      categoryId: 0,
      isActive: true,
    },
  })

  // Reset form when product changes or sheet opens
  useEffect(() => {
    if (open) {
      if (product) {
        form.reset({
          name: product.name,
          slug: product.slug,
          description: '',
          price: product.price,
          stock: product.stock,
          categoryId: 0,
          isActive: product.isActive,
        })
      } else {
        form.reset({
          name: '',
          slug: '',
          description: '',
          price: 0,
          stock: 0,
          categoryId: 0,
          isActive: true,
        })
      }
    }
  }, [open, product, form])

  const handleSubmit = form.handleSubmit((values) => {
    onSubmit(values)
  })

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="sm:max-w-lg overflow-y-auto">
        <SheetHeader>
          <SheetTitle>
            {isEditing ? 'Edit Product' : 'Add New Product'}
          </SheetTitle>
          <SheetDescription>
            {isEditing
              ? 'Update the product details below.'
              : 'Fill in the details to create a new product.'}
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit} className="space-y-4 px-4">
          {/* Product Name */}
          <div className="space-y-2">
            <Label htmlFor="name">Product Name</Label>
            <Input
              id="name"
              placeholder="e.g. Wireless Headphones"
              {...form.register('name')}
              aria-invalid={!!form.formState.errors.name}
            />
            {form.formState.errors.name && (
              <p className="text-sm text-destructive">
                {form.formState.errors.name.message}
              </p>
            )}
          </div>

          {/* Description */}
          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <textarea
              id="description"
              rows={3}
              placeholder="Describe the product..."
              className="flex min-h-[80px] w-full rounded-md border border-input bg-transparent px-3 py-2 text-base shadow-xs placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 md:text-sm"
              {...form.register('description')}
              aria-invalid={!!form.formState.errors.description}
            />
            {form.formState.errors.description && (
              <p className="text-sm text-destructive">
                {form.formState.errors.description.message}
              </p>
            )}
          </div>

          {/* Price & Stock */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="price">Price ($)</Label>
              <Input
                id="price"
                type="number"
                step="0.01"
                min="0.01"
                {...form.register('price', { valueAsNumber: true })}
                aria-invalid={!!form.formState.errors.price}
              />
              {form.formState.errors.price && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.price.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="stock">Stock</Label>
              <Input
                id="stock"
                type="number"
                min="0"
                {...form.register('stock', { valueAsNumber: true })}
                aria-invalid={!!form.formState.errors.stock}
              />
              {form.formState.errors.stock && (
                <p className="text-sm text-destructive">
                  {form.formState.errors.stock.message}
                </p>
              )}
            </div>
          </div>

          {/* Category */}
          <div className="space-y-2">
            <Label htmlFor="category-select">Category</Label>
            <Select
              value={String(form.watch('categoryId') || '')}
              onValueChange={(value) =>
                form.setValue('categoryId', Number(value), {
                  shouldValidate: true,
                })
              }
            >
              <SelectTrigger id="category-select" className="w-full">
                <SelectValue placeholder="Select a category" />
              </SelectTrigger>
              <SelectContent>
                {categories.map((cat) => (
                  <SelectItem key={cat.id} value={String(cat.id)}>
                    {cat.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {form.formState.errors.categoryId && (
              <p className="text-sm text-destructive">
                {form.formState.errors.categoryId.message}
              </p>
            )}
          </div>

          {/* Active toggle */}
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isActive"
              className="h-4 w-4 rounded border"
              {...form.register('isActive')}
            />
            <Label htmlFor="isActive" className="cursor-pointer">
              Active (visible to customers)
            </Label>
          </div>

          <SheetFooter>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting
                ? 'Saving...'
                : isEditing
                  ? 'Update Product'
                  : 'Save Product'}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  )
}
