import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type {
  ApiResponse,
  AddressDto,
  CreateAddressRequest,
  UpdateAddressRequest,
} from './types'

const ADDRESSES_KEY = ['addresses']

/** Fetch all addresses for the current customer. */
export function useAddresses() {
  return useQuery({
    queryKey: ADDRESSES_KEY,
    queryFn: async () => {
      const { data } = await api.get<ApiResponse<AddressDto[]>>('/addresses')
      return data
    },
  })
}

/** Create a new address. Invalidates the addresses query on success. */
export function useCreateAddress() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (request: CreateAddressRequest) => {
      const { data } = await api.post<ApiResponse<AddressDto>>('/addresses', request)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADDRESSES_KEY })
    },
  })
}

/** Update an existing address. Invalidates the addresses query on success. */
export function useUpdateAddress() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, request }: { id: number; request: UpdateAddressRequest }) => {
      const { data } = await api.put<ApiResponse<AddressDto>>(`/addresses/${id}`, request)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADDRESSES_KEY })
    },
  })
}

/** Delete an address. Invalidates the addresses query on success. */
export function useDeleteAddress() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: number) => {
      const { data } = await api.delete<ApiResponse<void>>(`/addresses/${id}`)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADDRESSES_KEY })
    },
  })
}

/** Set an address as the default. Invalidates the addresses query on success. */
export function useSetDefaultAddress() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: number) => {
      const { data } = await api.put<ApiResponse<void>>(`/addresses/${id}/default`, {})
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ADDRESSES_KEY })
    },
  })
}

/** Returns the default address, or undefined if none exists. */
export function useDefaultAddress(): AddressDto | undefined {
  const { data } = useAddresses()
  return data?.data?.find((a) => a.isDefault)
}
