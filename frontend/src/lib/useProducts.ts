import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { ApiResponse, ProductListItem, ProductDetailDto, CategoryDto } from './types'

export interface ProductListParams {
  page?: number
  pageSize?: number
  category?: string
  search?: string
  sort?: string
}

export function useProducts(params: ProductListParams = {}) {
  return useQuery({
    queryKey: ['products', params],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<ProductListItem[]>>('/products', { params })
      return data
    },
  })
}

export function useProduct(id: number) {
  return useQuery({
    queryKey: ['product', id],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<ProductDetailDto>>(`/products/${id}`)
      return data
    },
    enabled: id > 0,
  })
}

export function useCategories() {
  return useQuery({
    queryKey: ['categories'],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<CategoryDto[]>>('/categories')
      return data
    },
  })
}
