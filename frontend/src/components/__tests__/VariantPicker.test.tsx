import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { VariantPicker } from '@/components/VariantPicker'
import type { ProductVariantDto } from '@/lib/types'

const variants: ProductVariantDto[] = [
  { id: 10, sku: 'SKU-M-BLK', size: 'M', color: 'Black', stock: 5, isActive: true },
  { id: 11, sku: 'SKU-M-WHT', size: 'M', color: 'White', stock: 0, isActive: true },
  { id: 12, sku: 'SKU-L-BLK', size: 'L', color: 'Black', stock: 3, isActive: true },
]

describe('VariantPicker', () => {
  it('renders size and color groups', () => {
    render(<VariantPicker variants={variants} selectedId={10} onSelect={vi.fn()} />)
    expect(screen.getByText('Size')).toBeInTheDocument()
    expect(screen.getByText('Color')).toBeInTheDocument()
  })

  it('marks the selected variant as pressed', () => {
    render(<VariantPicker variants={variants} selectedId={10} onSelect={vi.fn()} />)
    expect(screen.getByRole('button', { name: 'Size M' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'Color Black' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('calls onSelect when a size is clicked', async () => {
    const user = userEvent.setup()
    const onSelect = vi.fn()
    render(<VariantPicker variants={variants} selectedId={10} onSelect={onSelect} />)
    await user.click(screen.getByRole('button', { name: 'Size L' }))
    expect(onSelect).toHaveBeenCalledWith(12)
  })

  it('disables out-of-stock axes and shows stock hint', () => {
    render(<VariantPicker variants={variants} selectedId={10} onSelect={vi.fn()} />)
    // White only has the M/WHT variant which is stock 0 -> the color button is disabled when all its variants are OOS
    // In this fixture M-White is the only White variant and it's 0, so Color White should be disabled
    expect(screen.getByRole('button', { name: 'Color White' })).toBeDisabled()
    expect(screen.getByText(/Selected: SKU-M-BLK/)).toBeInTheDocument()
  })

  it('renders nothing when variants is empty', () => {
    const { container } = render(<VariantPicker variants={[]} selectedId={null} onSelect={vi.fn()} />)
    expect(container).toBeEmptyDOMElement()
  })
})
