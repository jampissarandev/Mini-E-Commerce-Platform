/// <reference types="vitest" />
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Match the Vite path alias so `import { ... } from '@/lib/...'` works in tests.
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
    css: false,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      // Don't count config / test files in coverage
      exclude: [
        'node_modules/',
        'src/test/',
        '**/*.test.{ts,tsx}',
        '**/*.config.{ts,js}',
        '**/types.ts',
      ],
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
})
