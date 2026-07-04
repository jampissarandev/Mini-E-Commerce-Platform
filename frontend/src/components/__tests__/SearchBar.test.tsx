import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { SearchBar } from '@/components/SearchBar'

function renderSearchBar(defaultValue = '') {
  const onSearch = vi.fn()
  return {
    onSearch,
    ...render(
      <MemoryRouter initialEntries={[`/products?search=${defaultValue}`]}>
        <SearchBar defaultValue={defaultValue} onSearch={onSearch} />
      </MemoryRouter>,
    ),
  }
}

describe('SearchBar', () => {
  it('renders a search input', () => {
    renderSearchBar()
    expect(screen.getByRole('searchbox', { name: /search products/i })).toBeInTheDocument()
  })

  it('renders with a default value', () => {
    renderSearchBar('laptop')
    expect(screen.getByRole('searchbox')).toHaveValue('laptop')
  })

  it('calls onSearch when form is submitted', () => {
    const { onSearch } = renderSearchBar()
    const input = screen.getByRole('searchbox')
    fireEvent.change(input, { target: { value: 'phone' } })
    fireEvent.submit(input.closest('form')!)
    expect(onSearch).toHaveBeenCalledWith('phone')
  })

  it('calls onSearch with empty string when input is cleared', () => {
    const { onSearch } = renderSearchBar('laptop')
    const input = screen.getByRole('searchbox')
    fireEvent.change(input, { target: { value: '' } })
    fireEvent.submit(input.closest('form')!)
    expect(onSearch).toHaveBeenCalledWith('')
  })

  it('has a clear button when value is present', () => {
    renderSearchBar('laptop')
    expect(screen.getByRole('button', { name: /clear/i })).toBeInTheDocument()
  })

  it('clears the input and calls onSearch when clear is clicked', () => {
    const { onSearch } = renderSearchBar('laptop')
    fireEvent.click(screen.getByRole('button', { name: /clear/i }))
    expect(onSearch).toHaveBeenCalledWith('')
  })
})
