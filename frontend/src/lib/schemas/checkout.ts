import { z } from 'zod'

// Shipping fields are required when no saved address is selected (the body
// fields are the source of truth for the snapshot). When addressId is set,
// the backend snapshots from the saved address and ignores the body fields,
// so they may be empty. saveAddress lets the customer persist a one-off
// address to their book (ADR 0004).
export const checkoutSchema = z
  .object({
    addressId: z.number().optional(),
    fullName: z.string(),
    street: z.string(),
    city: z.string(),
    postalCode: z.string(),
    country: z.string(),
    phone: z.string(),
    saveAddress: z.boolean(),
  })
  .superRefine((values, ctx) => {
    if (values.addressId !== undefined) return
    const checks: ReadonlyArray<readonly [keyof Omit<typeof values, 'addressId' | 'saveAddress'>, string, number]> = [
      ['fullName', 'Full name is required.', 2],
      ['street', 'Street is required.', 3],
      ['city', 'City is required.', 2],
      ['postalCode', 'Postal code is required.', 3],
      ['country', 'Country is required.', 2],
      ['phone', 'Phone is required.', 5],
    ]
    for (const [field, message, min] of checks) {
      const v = values[field]
      if (v.length < min) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: [field],
          message,
        })
      }
    }
  })

export type CheckoutValues = z.infer<typeof checkoutSchema>
