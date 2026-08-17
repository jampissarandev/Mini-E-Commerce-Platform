// Default MSW request handlers. Anything not matched here will throw
// (onUnhandledRequest: 'error' in setup.ts) — that catches accidental
// real network calls during tests.
//
// Add a handler here for every API the app calls by default (e.g. the
// /api/health probe) so component smoke tests don't have to set up state.

import { http, HttpResponse } from 'msw'

const baseUrl = 'http://localhost:5173' // matches the Vite dev server origin

export const handlers = [
  // The Vite proxy rewrites /api/* → http://localhost:5000/*.
  // jsdom doesn't run Vite, so MSW sees the full URL instead.
  http.get(`${baseUrl}/api/health`, () =>
    HttpResponse.json({ status: 'ok' }),
  ),

  // Product catalog endpoints — use regex to avoid matching /products/:id
  http.get(/\/api\/products$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
      meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
    }),
  ),

  http.get(/\/api\/categories$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
    }),
  ),

  // Cart endpoint — default empty cart so components that render CartBadge
  // (like Navbar) don't need per-test overrides.
  http.get(/\/api\/cart$/, () =>
    HttpResponse.json({
      success: true,
      data: {
        id: 1,
        createdAt: '2026-01-01T00:00:00Z',
        updatedAt: '2026-01-01T00:00:00Z',
        items: [],
        total: 0,
      },
    }),
  ),

  // Order endpoints
  http.get(/\/api\/orders$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
      meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
    }),
  ),

  // Default mock-payment mode is AlwaysSucceed so the Checkout page does
  // not show a dev banner unless a test opts in to a failure-injection mode.
  http.get(/\/api\/payments\/mock-mode$/, () =>
    HttpResponse.json({
      success: true,
      data: { mode: 'AlwaysSucceed', failIfAmountGreaterThan: null },
    }),
  ),

  // ─── Admin product endpoints ───────────────────────────────
  http.get(/\/api\/admin\/products$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
      meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
    }),
  ),

  http.post(/\/api\/admin\/products$/, () =>
    HttpResponse.json(
      {
        success: true,
        data: {
          id: 99,
          name: 'New Product',
          slug: 'new-product',
          description: 'A new product',
          price: 19.99,
          stock: 10,
          isActive: true,
          createdAt: '2026-07-04T00:00:00Z',
          category: { id: 1, name: 'Electronics', slug: 'electronics' },
          images: [],
        },
      },
      { status: 201 },
    ),
  ),

  // ─── Admin dashboard endpoints ─────────────────────────────
  // Default empty-ish data so the AdminDashboard page renders without
  // per-test setup. Tests override these with server.use(...).
  http.get(/\/api\/admin\/dashboard\/summary$/, () =>
    HttpResponse.json({
      success: true,
      data: {
        totalOrders: 0,
        totalRevenue: 0,
        totalCustomers: 0,
        totalProducts: 0,
        lowStockCount: 0,
      },
    }),
  ),

  http.get(/\/api\/admin\/dashboard\/sales$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
    }),
  ),

  http.get(/\/api\/admin\/dashboard\/recent-orders$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
    }),
  ),

  http.get(/\/api\/admin\/dashboard\/low-stock$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
    }),
  ),

  // ─── Admin order endpoints ─────────────────────────────────
  // Default empty list so the AdminOrders page renders without per-test setup.
  http.get(/\/api\/admin\/orders$/, () =>
    HttpResponse.json({
      success: true,
      data: [],
      meta: { page: 1, pageSize: 10, totalCount: 0, totalPages: 0 },
    }),
  ),

  // Default detail so the AdminOrderDetail page renders without per-test setup.
  http.get(/\/api\/admin\/orders\/\d+$/, () =>
    HttpResponse.json({
      success: true,
      data: {
        id: 1,
        status: 'Pending',
        subtotal: 100,
        shippingFee: 10,
        total: 110,
        shippingFullName: 'Jane Doe',
        shippingStreet: '123 Main St',
        shippingCity: 'Springfield',
        shippingPostalCode: '12345',
        shippingCountry: 'USA',
        shippingPhone: '555-1234',
        createdAt: '2026-07-10T10:00:00Z',
        customer: {
          id: 'cust-1',
          email: 'jane@example.com',
          fullName: 'Jane Doe',
        },
        items: [
          {
            id: 1,
            productId: 1,
            productName: 'Laptop',
            unitPrice: 100,
            quantity: 1,
            imageUrl: '/images/laptop.jpg',
            subtotal: 100,
          },
        ],
        allowedNextStatuses: ['Paid', 'Cancelled'],
      },
    }),
  ),
]
