import { render, screen, fireEvent } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { Pagination } from '@/components/Pagination'

describe('Pagination', () => {
  it('renders page buttons', () => {
    render(<Pagination currentPage={1} totalPages={5} onPageChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /page 1/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /page 5/i })).toBeInTheDocument()
  })

  it('highlights the current page', () => {
    render(<Pagination currentPage={2} totalPages={5} onPageChange={vi.fn()} />)
    const currentBtn = screen.getByRole('button', { name: /page 2/i })
    expect(currentBtn).toHaveAttribute('aria-current', 'page')
  })

  it('disables previous button on first page', () => {
    render(<Pagination currentPage={1} totalPages={5} onPageChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled()
  })

  it('disables next button on last page', () => {
    render(<Pagination currentPage={5} totalPages={5} onPageChange={vi.fn()} />)
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled()
  })

  it('calls onPageChange when a page button is clicked', () => {
    const onChange = vi.fn()
    render(<Pagination currentPage={1} totalPages={5} onPageChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /page 3/i }))
    expect(onChange).toHaveBeenCalledWith(3)
  })

  it('calls onPageChange with previous page', () => {
    const onChange = vi.fn()
    render(<Pagination currentPage={3} totalPages={5} onPageChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /previous/i }))
    expect(onChange).toHaveBeenCalledWith(2)
  })

  it('calls onPageChange with next page', () => {
    const onChange = vi.fn()
    render(<Pagination currentPage={3} totalPages={5} onPageChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /next/i }))
    expect(onChange).toHaveBeenCalledWith(4)
  })

  it('does not render when totalPages is 1', () => {
    const { container } = render(<Pagination currentPage={1} totalPages={1} onPageChange={vi.fn()} />)
    expect(container.firstChild).toBeNull()
  })
})
