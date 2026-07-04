import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { CategoryFilter } from '@/components/CategoryFilter'
import type { CategoryDto } from '@/lib/types'

const mockCategories: CategoryDto[] = [
  { id: 1, name: 'Electronics', slug: 'electronics', productCount: 5 },
  { id: 2, name: 'Books', slug: 'books', productCount: 3 },
  { id: 3, name: 'Clothing', slug: 'clothing', productCount: 8 },
]

function renderFilter(selectedCategory?: string) {
  const onCategoryChange = vi.fn()
  return {
    onCategoryChange,
    ...render(
      <MemoryRouter initialEntries={['/products']}>
        <CategoryFilter
          categories={mockCategories}
          selectedCategory={selectedCategory}
          onCategoryChange={onCategoryChange}
        />
      </MemoryRouter>,
    ),
  }
}

describe('CategoryFilter', () => {
  it('renders all categories as buttons', () => {
    renderFilter()
    expect(screen.getByRole('button', { name: /all/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /electronics/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /books/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /clothing/i })).toBeInTheDocument()
  })

  it('highlights the selected category', () => {
    renderFilter('electronics')
    const selected = screen.getByRole('button', { name: /electronics/i })
    expect(selected).toHaveAttribute('aria-pressed', 'true')
  })

  it('calls onCategoryChange when a category is clicked', () => {
    const { onCategoryChange } = renderFilter()
    fireEvent.click(screen.getByRole('button', { name: /books/i }))
    expect(onCategoryChange).toHaveBeenCalledWith('books')
  })

  it('calls onCategoryChange with undefined when "All" is clicked', () => {
    const { onCategoryChange } = renderFilter('electronics')
    fireEvent.click(screen.getByRole('button', { name: /all/i }))
    expect(onCategoryChange).toHaveBeenCalledWith(undefined)
  })

  it('shows product count for each category', () => {
    renderFilter()
    expect(screen.getByText('(5)')).toBeInTheDocument()
    expect(screen.getByText('(3)')).toBeInTheDocument()
    expect(screen.getByText('(8)')).toBeInTheDocument()
  })
})
