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

// Cart types — mirrors backend CartDto / CartItemDto.
export interface CartItemDto {
  id: number
  productId: number
  productName: string
  productSlug: string
  imageUrl: string
  unitPrice: number
  quantity: number
  subtotal: number
}

export interface CartDto {
  id: number
  createdAt: string
  updatedAt: string
  items: CartItemDto[]
  total: number
}

export interface AddCartItemRequest {
  productId: number
  quantity: number
}

export interface UpdateCartItemRequest {
  quantity: number
}

// Order types — mirrors backend OrderDto / OrderItemDto / CheckoutRequest.
export interface CheckoutRequest {
  fullName: string
  street: string
  city: string
  postalCode: string
  country: string
  phone: string
}

export interface OrderItemDto {
  id: number
  productId: number
  productName: string
  unitPrice: number
  quantity: number
  subtotal: number
}

export interface OrderDto {
  id: number
  status: string
  subtotal: number
  shippingFee: number
  total: number
  shippingFullName: string
  shippingStreet: string
  shippingCity: string
  shippingPostalCode: string
  shippingCountry: string
  shippingPhone: string
  createdAt: string
  items: OrderItemDto[]
}
