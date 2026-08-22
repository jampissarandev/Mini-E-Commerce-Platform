import { describe, expect, it } from 'vitest'
import type { ProductVariantDto } from '@/lib/types'
import { pickPreferredVariant } from '@/lib/variant-utils'

function v(
  id: number,
  overrides: Partial<ProductVariantDto> = {},
): ProductVariantDto {
  return {
    id,
    sku: `SKU-${id}`,
    size: null,
    color: null,
    stock: 0,
    isActive: true,
    ...overrides,
  }
}

describe('pickPreferredVariant', () => {
  it('returns null for an empty list', () => {
    expect(pickPreferredVariant([])).toBeNull()
  })

  it('returns null when no variant is active', () => {
    const variants = [
      v(1, { isActive: false, stock: 5 }),
      v(2, { isActive: false, stock: 3 }),
    ]
    expect(pickPreferredVariant(variants)).toBeNull()
  })

  it('prefers the first in-stock active variant', () => {
    const variants = [
      v(1, { stock: 0 }),
      v(2, { stock: 5 }),
      v(3, { stock: 10 }),
    ]
    expect(pickPreferredVariant(variants)?.id).toBe(2)
  })

  it('falls back to the first active variant when none are in stock', () => {
    const variants = [
      v(1, { stock: 0 }),
      v(2, { stock: 0 }),
    ]
    expect(pickPreferredVariant(variants)?.id).toBe(1)
  })

  it('ignores inactive variants even when they are in stock', () => {
    const variants = [
      v(1, { isActive: false, stock: 100 }),
      v(2, { isActive: true, stock: 5 }),
    ]
    expect(pickPreferredVariant(variants)?.id).toBe(2)
  })
})
