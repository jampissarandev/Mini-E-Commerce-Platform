import type { ProductVariantDto } from '@/lib/types'

/**
 * Pick the preferred variant for a product: prefer the first active variant
 * that is in stock; fall back to the first active variant (so the picker
 * never dead-ends); return null if there are no active variants.
 *
 * Extracted from the anonymous IIFE in `ProductDetail.tsx` so it can be
 * pinned by unit tests and reused (e.g. when a default-variant policy for
 * `ProductCard` quick-add lands).
 */
export function pickPreferredVariant(
  variants: ProductVariantDto[],
): ProductVariantDto | null {
  if (variants.length === 0) return null
  const active = variants.filter((v) => v.isActive)
  return active.find((v) => v.stock > 0) ?? active[0] ?? null
}
