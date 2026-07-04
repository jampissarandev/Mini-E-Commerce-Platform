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
]
