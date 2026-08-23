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

/**
 * A neutral CartItemDto placeholder for the optimistic patch. The cart
 * badge only sums quantities, so unknown fields (name, price, image) stay
 * empty; the invalidation refetch replaces the placeholder with the real
 * server row moments later.
 */
function optimisticItem(request: AddCartItemRequest): CartItemDto {
  return {
    id: -Date.now(), // negative = client-side placeholder, never a real row id
    productId: 0,
    productVariantId: request.productVariantId,
    productName: '',
    productSlug: '',
    size: null,
    color: null,
    imageUrl: '',
    unitPrice: 0,
    quantity: request.quantity,
    subtotal: 0,
  }
}

interface AddToCartSnapshot {
  previous?: ApiResponse<CartDto>
}

/** Add an item to the cart. Optimistically patches the cached cart so the
 *  badge count updates instantly (ADR 0008 Phase C, Q6); rolls back on
 *  failure and refetches the truth on success. */
export function useAddToCart() {
  const queryClient = useQueryClient()
  return useMutation<ApiResponse<CartItemDto>, Error, AddCartItemRequest, AddToCartSnapshot>({
    mutationFn: async (request: AddCartItemRequest) => {
      const { data } = await api.post<ApiResponse<CartItemDto>>('/cart/items', request)
      return data
    },
    onMutate: async (request) => {
      // Cancel in-flight cart refetches so they can't clobber the patch.
      await queryClient.cancelQueries({ queryKey: CART_KEY })
      const previous = queryClient.getQueryData<ApiResponse<CartDto>>(CART_KEY)
      if (previous?.data) {
        queryClient.setQueryData<ApiResponse<CartDto>>(CART_KEY, {
          ...previous,
          data: {
            ...previous.data,
            items: [...previous.data.items, optimisticItem(request)],
          },
        })
      }
      return { previous }
    },
    onError: (_error, _request, context) => {
      if (context?.previous) {
        queryClient.setQueryData(CART_KEY, context.previous)
      }
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
