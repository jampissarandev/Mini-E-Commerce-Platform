// Global test setup. Runs once per test file before any test is executed.
//
// What it does:
//   1. Extends Vitest with @testing-library/jest-dom matchers
//      (e.g. `expect(el).toBeInTheDocument()`).
//   2. Starts the MSW request interceptor so all network calls made under
//      jsdom are routed to our handlers, not the real network.
//   3. Cleans up interceptors after each test.
//
// See https://testing-library.com/docs/react-testing-library/setup and
// https://mswjs.io/docs/integrations/browser for the patterns below.

import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './server'

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
