import { z } from 'zod'

export const adminProductSchema = z.object({
  name: z
    .string()
    .min(1, 'Product name is required.')
    .max(200, 'Product name must be 200 characters or fewer.'),
  slug: z.string().optional(),
  description: z
    .string()
    .min(1, 'Description is required.'),
  price: z
    .number({ message: 'Price must be a number.' })
    .min(0.01, 'Price must be at least $0.01.')
    .max(999_999.99, 'Price must be less than $1,000,000.'),
  stock: z
    .number({ message: 'Stock must be a number.' })
    .int('Stock must be a whole number.')
    .min(0, 'Stock cannot be negative.'),
  categoryId: z
    .number({ message: 'Category is required.' })
    .min(1, 'Please select a category.'),
  isActive: z.boolean(),
})

export type AdminProductValues = z.infer<typeof adminProductSchema>
