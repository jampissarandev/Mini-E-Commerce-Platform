import { Link } from 'react-router-dom'
import { Pencil } from 'lucide-react'
import type { LowStockProductDto } from '@/lib/types'

interface LowStockTableProps {
  products: LowStockProductDto[]
}

/**
 * Low-stock products table for the admin dashboard (Task 17c). Renders
 * product name + stock with an Edit link to the product management page.
 * The parent owns loading and empty states.
 */
export function LowStockTable({ products }: LowStockTableProps) {
  return (
    <div className="overflow-x-auto rounded-lg border">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b bg-muted/50 text-left">
            <th className="px-4 py-3 font-medium">Product</th>
            <th className="px-4 py-3 font-medium text-right">Stock</th>
            <th className="px-4 py-3 font-medium" aria-label="Actions" />
          </tr>
        </thead>
        <tbody>
          {products.map((product) => (
            <tr
              key={product.id}
              className="border-b last:border-b-0 hover:bg-muted/40"
            >
              <td className="px-4 py-3 font-medium">{product.name}</td>
              <td className="px-4 py-3 text-right">
                <span
                  className={
                    product.stock === 0
                      ? 'font-semibold text-destructive'
                      : 'text-muted-foreground'
                  }
                >
                  {product.stock}
                </span>
              </td>
              <td className="px-4 py-3 text-right">
                <Link
                  to="/admin/products"
                  aria-label={`Edit ${product.name}`}
                  className="inline-flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                >
                  <Pencil className="h-4 w-4" />
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}