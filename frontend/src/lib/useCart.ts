import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type { ApiResponse, CartDto, CartItemDto, AddCartItemRequest, UpdateCartItemRequest } from './types'

const CART_KEY = ['cart']

/** Fetch the current user's cart. */
export function useCart() {
  return useQuery({
    queryKey: CART_KEY,
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<CartDto>>('/cart')
      return data
    },
  })
}

/** Add an item to the cart. Invalidates the cart query on success. */
export function useAddToCart() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (request: AddCartItemRequest) => {
      const { data } = await api.post<ApiResponse<CartItemDto>>('/cart/items', request)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CART_KEY })
    },
  })
}

/** Update the quantity of a cart item. Invalidates the cart query on success. */
export function useUpdateCartItem() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, quantity }: { id: number; quantity: number }) => {
      const { data } = await api.put<ApiResponse<CartItemDto>>(`/cart/items/${id}`, {
        quantity,
      } satisfies UpdateCartItemRequest)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CART_KEY })
    },
  })
}

/** Remove a single item from the cart. Invalidates the cart query on success. */
export function useRemoveCartItem() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: number) => {
      const { data } = await api.delete<ApiResponse<void>>(`/cart/items/${id}`)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CART_KEY })
    },
  })
}

/** Clear all items from the cart. Invalidates the cart query on success. */
export function useClearCart() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.delete<ApiResponse<void>>('/cart')
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CART_KEY })
    },
  })
}

/** Returns the total number of individual items (sum of quantities) in the cart. */
export function useCartItemCount(): number {
  const { data } = useCart()
  return data?.data.items.reduce((sum, item) => sum + item.quantity, 0) ?? 0
}
