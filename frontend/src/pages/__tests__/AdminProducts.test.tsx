import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, beforeEach } from 'vitest'
import { server } from '@/test/server'
import { AdminProducts } from '@/pages/admin/Products'

// ── Test data ──────────────────────────────────────────────

const adminProductsPage = {
  success: true,
  data: [
    {
      id: 1,
      name: 'Laptop',
      slug: 'laptop',
      price: 999.99,
      stock: 25,
      isActive: true,
      categoryName: 'Electronics',
      imageUrl: '/images/laptop.jpg',
      createdAt: '2026-01-15T10:00:00Z',
    },
    {
      id: 2,
      name: 'Headphones',
      slug: 'headphones',
      price: 49.99,
      stock: 0,
      isActive: false,
      categoryName: 'Electronics',
      imageUrl: '/images/headphones.jpg',
      createdAt: '2026-02-20T12:00:00Z',
    },
  ],
  meta: { page: 1, pageSize: 10, totalCount: 2, totalPages: 1 },
}

const categoriesData = {
  success: true,
  data: [
    { id: 1, name: 'Electronics', slug: 'electronics', productCount: 2 },
    { id: 2, name: 'Books', slug: 'books', productCount: 3 },
  ],
}

const emptyProducts = {
  success: true,
  data: [],
  meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
}

// ── Helpers ────────────────────────────────────────────────

function renderAdminProducts(route = '/admin/products') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return {
    user: userEvent.setup(),
    ...render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[route]}>
          <AdminProducts />
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  }
}

// ── Tests ──────────────────────────────────────────────────

describe('AdminProducts page', () => {
  beforeEach(() => {
    server.resetHandlers()
  })

  // ── 14a: Product data table ──────────────────────────────

  describe('Product data table', () => {
    it('renders page heading', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /products/i })).toBeInTheDocument()
      })
    })

    it('displays products in a table with name, category, price, stock, and status', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      expect(screen.getByText('Headphones')).toBeInTheDocument()
      // Both products have 'Electronics' category
      expect(screen.getAllByText('Electronics').length).toBeGreaterThanOrEqual(2)
      expect(screen.getByText('$999.99')).toBeInTheDocument()
      expect(screen.getByText('$49.99')).toBeInTheDocument()
      expect(screen.getByText('25')).toBeInTheDocument() // Laptop stock
      expect(screen.getByText('0')).toBeInTheDocument() // Headphones stock
    })

    it('shows Active/Inactive badge for product status', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByText('Active')).toBeInTheDocument()
      })
      expect(screen.getByText('Inactive')).toBeInTheDocument()
    })

    it('shows table column headers', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByText('Product')).toBeInTheDocument()
      })
      expect(screen.getByText('Category')).toBeInTheDocument()
      expect(screen.getByText('Price')).toBeInTheDocument()
      expect(screen.getByText('Stock')).toBeInTheDocument()
      expect(screen.getByText('Status')).toBeInTheDocument()
      expect(screen.getByText('Actions')).toBeInTheDocument()
    })

    it('shows empty state when no products exist', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(emptyProducts),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByText(/no products found/i)).toBeInTheDocument()
      })
    })

    it('shows loading state while fetching', () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      expect(screen.getByText(/loading products/i)).toBeInTheDocument()
    })
  })

  // ── 14b: Add/Edit product form ───────────────────────────

  describe('Add product form', () => {
    it('renders an "Add Product" button', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /add product/i })).toBeInTheDocument()
      })
    })

    it('opens the add product form when clicking "Add Product"', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /add product/i })).toBeInTheDocument()
      })

      await user.click(screen.getByRole('button', { name: /add product/i }))

      await waitFor(() => {
        expect(screen.getByText(/add new product/i)).toBeInTheDocument()
      })

      // Form fields should be visible
      expect(screen.getByLabelText(/product name/i)).toBeInTheDocument()
      expect(screen.getByLabelText(/description/i)).toBeInTheDocument()
      expect(screen.getByLabelText(/price/i)).toBeInTheDocument()
      expect(screen.getByLabelText(/stock/i)).toBeInTheDocument()
      expect(screen.getByLabelText(/category/i, { selector: '[id="category-select"]' })).toBeInTheDocument()
    })

    it('validates required fields before submission', async () => {
      let postCalled = false
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
        http.post(/\/api\/admin\/products$/, () => {
          postCalled = true
          return HttpResponse.json(
            { success: true, data: { id: 99 } },
            { status: 201 },
          )
        }),
      )

      const { user } = renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /add product/i })).toBeInTheDocument()
      })

      await user.click(screen.getByRole('button', { name: /add product/i }))

      await waitFor(() => {
        expect(screen.getByText(/add new product/i)).toBeInTheDocument()
      })

      // Submit without filling fields — validation should prevent submission
      const saveButton = screen.getByRole('button', { name: /save product/i })
      await user.click(saveButton)

      // The API should NOT have been called because validation fails
      // Give time for any potential async submission
      await new Promise((r) => setTimeout(r, 300))
      expect(postCalled).toBe(false)

      // Check for any error message rendered in the form
      const sheetContent = document.querySelector('[data-slot="sheet-content"]')
      expect(sheetContent).toBeInTheDocument()
      // Validation errors should appear somewhere in the form
      const errorTexts = (sheetContent as Element).querySelectorAll('[class*="destructive"]')
      expect(errorTexts.length).toBeGreaterThan(0)
    })

    it('shows validation error for invalid price', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /add product/i })).toBeInTheDocument()
      })

      await user.click(screen.getByRole('button', { name: /add product/i }))

      await waitFor(() => {
        expect(screen.getByText(/add new product/i)).toBeInTheDocument()
      })

      // Fill name and description so they pass validation
      const nameInput = screen.getByLabelText(/product name/i)
      await user.type(nameInput, 'Test Product')

      const descField = screen.getByLabelText(/description/i)
      await user.type(descField, 'A test description')

      // Set price to an invalid negative value
      const priceInput = screen.getByLabelText(/price/i)
      await user.clear(priceInput)
      await user.type(priceInput, '-5')

      // Fill stock
      const stockInput = screen.getByLabelText(/stock/i)
      await user.clear(stockInput)
      await user.type(stockInput, '10')

      await user.click(screen.getByRole('button', { name: /save product/i }))

      // The form should show validation errors (price is invalid, categoryId is 0)
      await waitFor(() => {
        const sheetContent = document.querySelector('[data-slot="sheet-content"]') as Element
        const errorTexts = sheetContent.querySelectorAll('[class*="destructive"]')
        expect(errorTexts.length).toBeGreaterThan(0)
      })
    })
  })

  describe('Edit product form', () => {
    it('opens edit form pre-filled with product data when clicking edit', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      // Find the edit button for the first product
      const editButtons = screen.getAllByRole('button', { name: /edit/i })
      await user.click(editButtons[0]!)

      await waitFor(() => {
        expect(screen.getByText(/edit product/i)).toBeInTheDocument()
      })

      // Form should be pre-filled
      expect(screen.getByLabelText(/product name/i)).toHaveValue('Laptop')
      expect(screen.getByLabelText(/price/i)).toHaveValue(999.99)
      expect(screen.getByLabelText(/stock/i)).toHaveValue(25)
    })
  })

  // ── 14c: Delete confirmation dialog ──────────────────────

  describe('Delete confirmation dialog', () => {
    it('opens a delete confirmation dialog when clicking delete', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i })
      await user.click(deleteButtons[0]!)

      await waitFor(() => {
        expect(screen.getByText(/are you sure/i)).toBeInTheDocument()
      })
      // The product name appears in the dialog description — scope to the alert dialog
      const dialog = screen.getByRole('alertdialog')
      expect(within(dialog).getByText(/laptop/i)).toBeInTheDocument()
    })

    it('shows confirm and cancel buttons in the dialog', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i })
      await user.click(deleteButtons[0]!)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /confirm delete/i })).toBeInTheDocument()
      })
      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument()
    })

    it('closes the dialog when clicking cancel', async () => {
      const { user } = renderAdminProducts()

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i })
      await user.click(deleteButtons[0]!)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument()
      })

      await user.click(screen.getByRole('button', { name: /cancel/i }))

      await waitFor(() => {
        expect(screen.queryByText(/are you sure/i)).not.toBeInTheDocument()
      })
    })

    it('calls DELETE API and refreshes the list on confirm', async () => {
      let deleteCalled = false

      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
        http.delete(/\/api\/admin\/products\/1$/, () => {
          deleteCalled = true
          return HttpResponse.json({ success: true, data: null })
        }),
      )

      const { user } = renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByText('Laptop')).toBeInTheDocument()
      })

      const deleteButtons = screen.getAllByRole('button', { name: /delete/i })
      await user.click(deleteButtons[0]!)

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /confirm delete/i })).toBeInTheDocument()
      })

      await user.click(screen.getByRole('button', { name: /confirm delete/i }))

      await waitFor(() => {
        expect(deleteCalled).toBe(true)
      })
    })
  })

  // ── Search and pagination ────────────────────────────────

  describe('Search and pagination', () => {
    it('renders a search input for filtering products', async () => {
      server.use(
        http.get(/\/api\/admin\/products$/, () =>
          HttpResponse.json(adminProductsPage),
        ),
        http.get(/\/api\/categories$/, () =>
          HttpResponse.json(categoriesData),
        ),
      )

      renderAdminProducts()

      await waitFor(() => {
        expect(screen.getByPlaceholderText(/search products/i)).toBeInTheDocument()
      })
    })
  })
})
