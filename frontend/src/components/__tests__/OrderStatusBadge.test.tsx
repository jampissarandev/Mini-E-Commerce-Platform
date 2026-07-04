import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OrderStatusBadge } from '@/components/OrderStatusBadge'

describe('OrderStatusBadge', () => {
  it('renders the human label for Paid', () => {
    render(<OrderStatusBadge status="Paid" />)
    expect(screen.getByText('Paid')).toBeInTheDocument()
  })

  it('renders the human label for Pending', () => {
    render(<OrderStatusBadge status="Pending" />)
    expect(screen.getByText('Pending')).toBeInTheDocument()
  })

  it('renders the human label for Shipped', () => {
    render(<OrderStatusBadge status="Shipped" />)
    expect(screen.getByText('Shipped')).toBeInTheDocument()
  })

  it('renders the human label for Delivered', () => {
    render(<OrderStatusBadge status="Delivered" />)
    expect(screen.getByText('Delivered')).toBeInTheDocument()
  })

  it('falls back to the raw status when unknown', () => {
    render(<OrderStatusBadge status="Cancelled" />)
    expect(screen.getByText('Cancelled')).toBeInTheDocument()
  })
})
