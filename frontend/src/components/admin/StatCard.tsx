import type { LucideIcon } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'

interface StatCardProps {
  title: string
  value: string
  icon?: LucideIcon
  hint?: string
}

/**
 * A single KPI card for the admin dashboard (Task 17b). Renders a title,
 * a formatted value, an optional icon, and an optional hint line. The value
 * is pre-formatted by the caller so this component stays presentational.
 */
export function StatCard({ title, value, icon: Icon, hint }: StatCardProps) {
  return (
    <Card>
      <CardContent className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <p className="text-sm font-medium text-muted-foreground">{title}</p>
          <p className="mt-1 truncate text-3xl font-bold tracking-tight">
            {value}
          </p>
          {hint && (
            <p className="mt-1 text-xs text-muted-foreground">{hint}</p>
          )}
        </div>
        {Icon && (
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-muted">
            <Icon className="h-6 w-6 text-muted-foreground" />
          </div>
        )}
      </CardContent>
    </Card>
  )
}