import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type {
  ApiResponse,
  AdminProductListItem,
  AdminProductDetailDto,
  CreateProductRequest,
  UpdateProductRequest,
  ProductImageDto,
} from './types'

export interface AdminProductListParams {
  page?: number
  pageSize?: number
  q?: string
  isActive?: boolean
}

export function useAdminProducts(params: AdminProductListParams = {}) {
  return useQuery({
    queryKey: ['admin-products', params],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AdminProductListItem[]>>(
        '/admin/products',
        { params },
      )
      return data
    },
  })
}

export function useAdminProduct(id: number) {
  return useQuery({
    queryKey: ['admin-product', id],
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AdminProductDetailDto>>(
        `/admin/products/${id}`,
      )
      return data
    },
    enabled: id > 0,
  })
}

export function useCreateProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (request: CreateProductRequest) => {
      const { data } = await api.post<ApiResponse<AdminProductDetailDto>>(
        '/admin/products',
        request,
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-products'] })
    },
  })
}

export function useUpdateProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      id,
      request,
    }: {
      id: number
      request: UpdateProductRequest
    }) => {
      const { data } = await api.put<ApiResponse<AdminProductDetailDto>>(
        `/admin/products/${id}`,
        request,
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-products'] })
    },
  })
}

export function useDeleteProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: number) => {
      const { data } = await api.delete<ApiResponse<null>>(
        `/admin/products/${id}`,
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-products'] })
    },
  })
}

export function useUploadProductImages() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      productId,
      files,
    }: {
      productId: number
      files: File[]
    }) => {
      const formData = new FormData()
      files.forEach((file) => formData.append('files', file))
      const { data } = await api.post<ApiResponse<ProductImageDto[]>>(
        `/admin/products/${productId}/images`,
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      return data
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ['admin-product', variables.productId],
      })
    },
  })
}

export function useDeleteProductImage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({
      productId,
      imageId,
    }: {
      productId: number
      imageId: number
    }) => {
      const { data } = await api.delete<ApiResponse<null>>(
        `/admin/products/${productId}/images/${imageId}`,
      )
      return data
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({
        queryKey: ['admin-product', variables.productId],
      })
    },
  })
}
