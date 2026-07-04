interface OrderStatusBadgeProps {
  status: string
  size?: 'sm' | 'md'
}

const STATUS_CONFIG: Record<string, { label: string; className: string }> = {
  Paid: { label: 'Paid', className: 'bg-green-100 text-green-800' },
  Pending: { label: 'Pending', className: 'bg-yellow-100 text-yellow-800' },
  Shipped: { label: 'Shipped', className: 'bg-blue-100 text-blue-800' },
  Delivered: { label: 'Delivered', className: 'bg-green-100 text-green-800' },
}

export function OrderStatusBadge({ status, size = 'md' }: OrderStatusBadgeProps) {
  const { label, className } = STATUS_CONFIG[status] ?? {
    label: status,
    className: 'bg-gray-100 text-gray-800',
  }

  const sizeClass =
    size === 'sm' ? 'px-2.5 py-0.5 text-xs' : 'px-3 py-1 text-sm'

  return (
    <span
      className={`inline-flex items-center rounded-full font-medium ${sizeClass} ${className}`}
    >
      {label}
    </span>
  )
}
