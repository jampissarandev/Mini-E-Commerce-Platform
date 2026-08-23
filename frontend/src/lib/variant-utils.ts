import type { ProductVariantDto } from '@/lib/types'

/**
 * Minimal shape pickPreferredVariant needs: an id to add, a stock count,
 * and — optionally — an active flag. The catalog list summary
 * (ProductVariantSummaryDto) omits `isActive` because the backend already
 * filters inactive variants out; full detail DTOs carry it.
 */
export type VariantLike = Pick<ProductVariantDto, 'id' | 'stock'> & {
  isActive?: boolean
}

/**
 * Pick the preferred variant for a product: prefer the first active variant
 * that is in stock; fall back to the first active variant (so the picker
 * never dead-ends); return null if there are no active variants.
 *
 * Extracted from the anonymous IIFE in `ProductDetail.tsx` so it can be
 * pinned by unit tests and reused by the `ProductCard` quick-add button
 * (ADR 0008 Phase C).
 */
export function pickPreferredVariant(
  variants: VariantLike[],
): VariantLike | null {
  if (variants.length === 0) return null
  const active = variants.filter((v) => v.isActive !== false)
  return active.find((v) => v.stock > 0) ?? active[0] ?? null
}
