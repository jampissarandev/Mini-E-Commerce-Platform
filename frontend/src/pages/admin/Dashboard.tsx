import {
  ShoppingCart,
  DollarSign,
  Users,
  AlertTriangle,
} from 'lucide-react'
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card'
import { StatCard } from '@/components/admin/StatCard'
import { SalesChart } from '@/components/admin/SalesChart'
import { RecentOrdersList } from '@/components/admin/RecentOrdersList'
import { LowStockTable } from '@/components/admin/LowStockTable'
import {
  useAdminDashboardSummary,
  useAdminDashboardSales,
  useAdminDashboardRecentOrders,
  useAdminDashboardLowStock,
} from '@/lib/useAdminDashboard'
import { formatCurrency, formatNumber } from '@/lib/utils'

const LOW_STOCK_THRESHOLD = 10

/**
 * Admin dashboard landing page (Task 17b + 17c). Replaces the placeholder:
 * 4 KPI cards from the summary endpoint, a 30-day sales chart, a recent-orders
 * list, and a low-stock table. Every section has its own loading + empty state
 * so the page degrades gracefully.
 */
export function AdminDashboard() {
  const summary = useAdminDashboardSummary()
  const sales = useAdminDashboardSales(30)
  const recentOrders = useAdminDashboardRecentOrders(10)
  const lowStock = useAdminDashboardLowStock(LOW_STOCK_THRESHOLD)

  const summaryData = summary.data?.data

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Dashboard</h1>
      </div>

      {/* KPI cards (17b) */}
      {summary.isLoading ? (
        <div
          className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
          role="status"
          aria-label="Loading dashboard summary"
        >
          {Array.from({ length: 4 }).map((_, i) => (
            <Card key={i} aria-hidden="true">
              <CardContent className="space-y-3">
                <div className="h-4 w-24 animate-pulse rounded bg-muted" />
                <div className="h-8 w-32 animate-pulse rounded bg-muted" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <StatCard
            title="Total Orders"
            value={formatNumber(summaryData?.totalOrders ?? 0)}
            icon={ShoppingCart}
          />
          <StatCard
            title="Revenue"
            value={formatCurrency(summaryData?.totalRevenue ?? 0)}
            icon={DollarSign}
          />
          <StatCard
            title="Customers"
            value={formatNumber(summaryData?.totalCustomers ?? 0)}
            icon={Users}
          />
          <StatCard
            title="Low Stock"
            value={formatNumber(summaryData?.lowStockCount ?? 0)}
            icon={AlertTriangle}
            hint={`Stock ≤ ${LOW_STOCK_THRESHOLD}`}
          />
        </div>
      )}

      {/* Sales chart (17c) */}
      <Card>
        <CardHeader>
          <CardTitle>Sales — Last 30 Days</CardTitle>
          <CardDescription>Daily revenue</CardDescription>
        </CardHeader>
        <CardContent>
          {sales.isLoading ? (
            <div
              className="flex h-72 items-center justify-center"
              role="status"
              aria-label="Loading sales chart"
            >
              <p className="text-muted-foreground">Loading sales chart...</p>
            </div>
          ) : (sales.data?.data.length ?? 0) === 0 ? (
            <div className="flex h-72 items-center justify-center">
              <p className="text-muted-foreground">No sales data yet</p>
            </div>
          ) : (
            <SalesChart data={sales.data?.data ?? []} />
          )}
        </CardContent>
      </Card>

      {/* Recent orders + low stock (17c) */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Recent Orders</CardTitle>
            <CardDescription>Latest activity</CardDescription>
          </CardHeader>
          <CardContent>
            {recentOrders.isLoading ? (
              <div
                className="flex items-center justify-center py-12"
                role="status"
                aria-label="Loading recent orders"
              >
                <p className="text-muted-foreground">Loading recent orders...</p>
              </div>
            ) : (recentOrders.data?.data.length ?? 0) === 0 ? (
              <div className="flex items-center justify-center py-12">
                <p className="text-muted-foreground">No orders yet</p>
              </div>
            ) : (
              <RecentOrdersList orders={recentOrders.data?.data ?? []} />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Low Stock</CardTitle>
            <CardDescription>
              Products at or below {LOW_STOCK_THRESHOLD} units
            </CardDescription>
          </CardHeader>
          <CardContent>
            {lowStock.isLoading ? (
              <div
                className="flex items-center justify-center py-12"
                role="status"
                aria-label="Loading low stock"
              >
                <p className="text-muted-foreground">Loading low stock...</p>
              </div>
            ) : (lowStock.data?.data.length ?? 0) === 0 ? (
              <div className="flex items-center justify-center py-12">
                <p className="text-muted-foreground">
                  All products are well stocked
                </p>
              </div>
            ) : (
              <LowStockTable products={lowStock.data?.data ?? []} />
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
