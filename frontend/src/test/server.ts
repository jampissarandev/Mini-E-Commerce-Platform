// MSW server instance for jsdom. See ./setup.ts for lifecycle wiring.
//
// Usage in a test:
//   import { http, HttpResponse } from 'msw'
//   import { server } from '@/test/server'
//
//   server.use(http.get('/api/products', () => HttpResponse.json([...])))
//
// Or, for a request that should be stubbed across all tests, add a handler
// to ./handlers.ts.

import { setupServer } from 'msw/node'
import { handlers } from './handlers'

export const server = setupServer(...handlers)
