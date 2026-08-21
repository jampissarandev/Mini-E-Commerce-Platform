import type { ProductVariantDto } from '@/lib/types'
import { Button } from '@/components/ui/button'

interface VariantPickerProps {
  variants: ProductVariantDto[]
  selectedId: number | null
  onSelect: (id: number) => void
}

// Small helper: returns sorted unique non-empty values for a given attribute
function uniq<T extends string | null | undefined>(values: T[]): string[] {
  return [...new Set(values.filter(Boolean) as string[])].sort()
}

export function VariantPicker({ variants, selectedId, onSelect }: VariantPickerProps) {
  if (variants.length === 0) return null

  const active = variants.filter((v) => v.isActive)
  if (active.length === 0) return null

  const sizes = uniq(active.map((v) => v.size))
  const colors = uniq(active.map((v) => v.color))
  const selected = active.find((v) => v.id === selectedId)

  // If neither size nor color vary, render a compact single-variant selector
  const hasSizeAxis = sizes.length > 0
  const hasColorAxis = colors.length > 0

  // Fallback: generic list when attributes are all null
  if (!hasSizeAxis && !hasColorAxis) {
    return (
      <div className="space-y-2" aria-label="Variant picker">
        <p className="text-sm font-medium">Options</p>
        <div className="flex flex-wrap gap-2">
          {active.map((v) => (
            <Button
              key={v.id}
              variant={v.id === selectedId ? 'default' : 'outline'}
              size="sm"
              onClick={() => onSelect(v.id)}
              aria-pressed={v.id === selectedId}
              aria-label={`Select variant ${v.sku}`}
              disabled={v.stock === 0}
            >
              {v.sku}
              {v.stock === 0 ? ' — Out of stock' : ''}
            </Button>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-4" aria-label="Variant picker">
      {hasSizeAxis && (
        <div className="space-y-2">
          <p className="text-sm font-medium">Size</p>
          <div className="flex flex-wrap gap-2">
            {sizes.map((size) => {
              const variantsForSize = active.filter((v) => v.size === size)
              const allOutOfStock = variantsForSize.every((v) => v.stock === 0)
              const isSelected = selected?.size === size
              return (
                <Button
                  key={size}
                  variant={isSelected ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => {
                    // Pick the first in-stock variant for this size (prefer matching current color)
                    const match =
                      (selected?.color
                        ? variantsForSize.find((v) => v.color === selected.color && v.stock > 0)
                        : undefined) ??
                      variantsForSize.find((v) => v.stock > 0) ??
                      variantsForSize[0]
                    if (match) onSelect(match.id)
                  }}
                  aria-pressed={isSelected}
                  aria-label={`Size ${size}`}
                  disabled={allOutOfStock}
                >
                  {size}
                </Button>
              )
            })}
          </div>
        </div>
      )}

      {hasColorAxis && (
        <div className="space-y-2">
          <p className="text-sm font-medium">Color</p>
          <div className="flex flex-wrap gap-2">
            {colors.map((color) => {
              const variantsForColor = active.filter((v) => v.color === color)
              const allOutOfStock = variantsForColor.every((v) => v.stock === 0)
              const isSelected = selected?.color === color
              return (
                <Button
                  key={color}
                  variant={isSelected ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => {
                    const match =
                      (selected?.size
                        ? variantsForColor.find((v) => v.size === selected.size && v.stock > 0)
                        : undefined) ??
                      variantsForColor.find((v) => v.stock > 0) ??
                      variantsForColor[0]
                    if (match) onSelect(match.id)
                  }}
                  aria-pressed={isSelected}
                  aria-label={`Color ${color}`}
                  disabled={allOutOfStock}
                >
                  {color}
                </Button>
              )
            })}
          </div>
        </div>
      )}

      {selected && (
        <p className="text-xs text-muted-foreground" aria-live="polite">
          Selected: {selected.sku}
          {selected.stock === 0 ? ' — Out of stock' : ` — ${selected.stock} in stock`}
        </p>
      )}
    </div>
  )
}
