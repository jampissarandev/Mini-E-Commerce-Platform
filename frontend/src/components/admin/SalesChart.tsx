import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from 'recharts'
import type { SalesPoint } from '@/lib/types'
import { formatCurrency } from '@/lib/utils'

interface SalesChartProps {
  data: SalesPoint[]
}

function formatAxisDate(dateStr: string): string {
  // SalesPoint.date is an ISO-8601 date (yyyy-MM-dd, UTC) — parse as UTC
  // midnight so the label never shifts a day due to local timezone.
  return new Date(`${dateStr}T00:00:00Z`).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  })
}

/**
 * 30-day sales line chart (Task 17c). Renders the daily revenue series from
 * the dashboard sales endpoint. Presentational — the parent owns loading and
 * empty states.
 */
export function SalesChart({ data }: SalesChartProps) {
  return (
    <div className="h-72 w-full" role="img" aria-label="Sales chart">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart
          data={data}
          margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
        >
          <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
          <XAxis
            dataKey="date"
            tickFormatter={formatAxisDate}
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-muted-foreground"
            minTickGap={24}
          />
          <YAxis
            tickFormatter={(v: number) => `$${v}`}
            tick={{ fontSize: 12 }}
            stroke="currentColor"
            className="text-muted-foreground"
            width={56}
          />
          <Tooltip
            formatter={(value) => [formatCurrency(Number(value)), 'Revenue']}
            labelFormatter={(label) => {
              const date = new Date(`${String(label)}T00:00:00Z`)
              return date.toLocaleDateString('en-US', {
                weekday: 'short',
                month: 'short',
                day: 'numeric',
              })
            }}
          />
          <Line
            type="monotone"
            dataKey="total"
            stroke="hsl(var(--primary))"
            strokeWidth={2}
            dot={false}
            activeDot={{ r: 4 }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}