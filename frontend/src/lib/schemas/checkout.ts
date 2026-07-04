import { z } from 'zod'

export const checkoutSchema = z.object({
  fullName: z
    .string()
    .min(2, 'Full name is required.'),
  street: z
    .string()
    .min(3, 'Street is required.'),
  city: z
    .string()
    .min(2, 'City is required.'),
  postalCode: z
    .string()
    .min(3, 'Postal code is required.'),
  country: z
    .string()
    .min(2, 'Country is required.'),
  phone: z
    .string()
    .min(5, 'Phone is required.'),
})

export type CheckoutValues = z.infer<typeof checkoutSchema>
