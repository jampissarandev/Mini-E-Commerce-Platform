// API response wrapper — mirrors backend ApiResponse<T>.
export interface ApiResponse<T> {
  success: boolean
  data: T
  error?: ApiError
  meta?: Meta
}

export interface ApiError {
  code: string
  message: string
  details?: Record<string, string[]>
}

export interface Meta {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

// Product catalog types — mirrors backend ProductListItem / ProductDetailDto.
export interface ProductListItem {
  id: number
  name: string
  slug: string
  price: number
  imageUrl: string
  categoryName: string
}

export interface ProductDetailDto {
  id: number
  name: string
  slug: string
  description: string
  price: number
  stock: number
  createdAt: string
  category: ProductCategoryDto
  images: ProductImageDto[]
}

export interface ProductCategoryDto {
  id: number
  name: string
  slug: string
}

export interface ProductImageDto {
  id: number
  url: string
  sortOrder: number
}

export interface CategoryDto {
  id: number
  name: string
  slug: string
  productCount: number
}
