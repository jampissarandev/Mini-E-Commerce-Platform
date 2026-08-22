// Renders a cart item's variant attributes in Color, Size order to match the
// OrderItem snapshot format (CONTEXT.md → OrderItem): "Name (Color, Size)".
// Returns null when both attributes are missing. The separator differs from
// the snapshot's ", " because cart rows are denser; pinning both formats here
// keeps clients from drifting on day one.
export function VariantAttributes({
  size,
  color,
  className,
}: {
  size?: string | null
  color?: string | null
  className?: string
}) {
  if (!size && !color) return null
  return (
    <p className={className ?? 'text-xs text-muted-foreground'}>
      {[color, size].filter(Boolean).join(' · ')}
    </p>
  )
}
