import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * Format a number as USD currency (e.g. 1234.5 -> "$1,234.50").
 * Shared by the admin dashboard cards, sales chart, and recent-orders list.
 */
export function formatCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value)
}

/** Format a number with thousands separators (e.g. 1234 -> "1,234"). */
export function formatNumber(value: number): string {
  return new Intl.NumberFormat("en-US").format(value)
}
