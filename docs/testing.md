# Testing Guide

> Single source of truth for **how** to run and **how** to write tests in this repo. The full test strategy lives in [`tasks/test-spec.md`](../tasks/test-spec.md) — this document is the practical, day-to-day guide.

---

## TL;DR

```bash
# Backend
cd backend
dotnet test                 # ~2 s, 27 tests

# Frontend
cd frontend
npm test                    # watch mode
npm run test:run            # one-shot CI mode, ~2 s, 9 tests
npm run test:coverage       # text + html coverage report
```

Both must be green before every commit.

---

## Backend (xUnit)

### Layout

```
backend/MiniEcommerce.Api.Tests/
├── Infrastructure/
│   ├── ApiFactory.cs              # WebApplicationFactory<Program>
│   ├── AssemblyConfig.cs          # sets JWT env var before builder runs
│   ├── DbContextExtensions.cs     # CreateDbContext + ResetDatabaseAsync
│   ├── HealthControllerTests.cs   # harness smoke test
│   └── IntegrationCollection.cs   # xUnit collection fixture
├── Unit/
│   ├── Middleware/ExceptionMiddlewareTests.cs
│   └── Repositories/RepositoryTests.cs
└── Integration/
    └── Controllers/AuthControllerTests.cs
```

### Conventions

- **Class name** = `{ClassUnderTest}Tests`.
- **Method name** = `Method_State_ExpectedBehavior`.
- **One assertion concept per test** (multiple `.Should()` calls on the same object are fine).
- Use `FluentAssertions` (`result.Should().Be(...)`).
- Pure unit tests (no HTTP) instantiate `Repository<T>` directly with a fresh InMemory `DbContext` per test.
- Integration tests use `[Collection(IntegrationCollection.Name)]` to share a single `ApiFactory` and call `await factory.ResetDatabaseAsync()` in `InitializeAsync` to isolate test state.

### In-memory DB caveats

- The EF Core InMemory provider does **not** support `ExecuteDeleteAsync` (bulk SQL DELETE). Use `RemoveRange` + `SaveChangesAsync` (already wrapped in `ResetDatabaseAsync`).
- It does **not** enforce relational constraints. Tests that depend on FK cascades need a real DB.
- It does **not** validate unique indexes. Assert on the application result (e.g. `IdentityError`), not on a `DbUpdateException`.

### Debugging

```bash
# Run a single test class
dotnet test --filter "FullyQualifiedName~AuthControllerTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~AuthControllerTests.Me_WithoutToken_Returns401"

# With detailed logs
dotnet test --logger "console;verbosity=detailed" --filter "FullyQualifiedName~..."
```

---

## Frontend (Vitest)

### Layout

```
frontend/
├── vitest.config.ts
└── src/
    ├── test/
    │   ├── setup.ts         # jest-dom matchers + MSW lifecycle
    │   ├── server.ts        # MSW Node server
    │   └── handlers.ts      # default handlers (e.g. /api/health)
    ├── lib/
    │   ├── utils.ts
    │   └── utils.test.ts    # colocated unit test
    └── App.test.tsx         # colocated smoke test
```

### Conventions

- **Test files colocated** with source (`Foo.ts` → `Foo.test.ts`).
- **Pure unit tests** (no React, no network) live in `*.test.ts`.
- **Component tests** live in `*.test.tsx` and render inside `<MemoryRouter>` + `<QueryClientProvider>` (see `App.test.tsx` for the pattern).
- **Network calls** are intercepted by MSW. Default handlers stub `/api/health`; per-test stubs use `server.use(http.get(...))`.
- Use `userEvent` for interactions (`await user.click(button)`), not `fireEvent`.

### Writing a component test

```tsx
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/server'
import { MyComponent } from './MyComponent'

it('shows a list of products', async () => {
  server.use(
    http.get('/api/products', () =>
      HttpResponse.json([{ id: 1, name: 'Foo' }]),
    ),
  )
  render(<MyComponent />)
  expect(await screen.findByText('Foo')).toBeInTheDocument()
})
```

### Vitest scripts

| Script | What it does |
|---|---|
| `npm test` | watch mode (default for local dev) |
| `npm run test:run` | one-shot, no watch (CI mode) |
| `npm run test:ui` | open Vitest UI in browser |
| `npm run test:coverage` | produce `coverage/` report |

---

## TDD Workflow

For every behavior change:

1. **Red** — write a failing test first. Name it `Method_State_ExpectedBehavior`.
2. **Green** — write the minimum production code to make it pass. No extras.
3. **Refactor** — clean up while tests stay green.
4. **Run the full suite** before committing. `dotnet test` + `npm run test:run` must both be green.

Forbidden:
- "I'll add tests later" — never merge behavior without a test.
- Asserting on `DateTime.Now` / `Guid.NewGuid()` directly — inject clocks / generators if you need determinism.
- `Thread.Sleep` in tests — use polling or fakes.

---

## CI

`.github/workflows/ci.yml` runs on every push and PR:

1. `dotnet test` (backend)
2. `npm ci && npm run lint && npm run test:run` (frontend)

Both must pass to merge. See [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) for the current pipeline.
