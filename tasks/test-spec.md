# Test Strategy & Specification

> Defines **how** we test, **what** we test, and the **workflow** we follow. Implementation is broken down in `tasks/todo.md` under Task 21.

---

## 1. Goals

1. Make TDD the default workflow for every behavior change in this repo.
2. Keep the test suite **fast** (whole backend suite < 10 s; whole frontend suite < 15 s) so developers actually run it on every change.
3. Catch regressions in business rules (auth, role gating, stock deduction, payment) **before** code review.
4. Tests must be **deterministic** — no network, no clock skew, no shared mutable state between tests.

## 2. Non-goals

- Full end-to-end browser tests (Playwright) — deferred to a follow-up.
- Coverage gates with hard thresholds — tooling is set up, policy is the team's call.
- Testing the unimplemented features (catalog, cart, admin APIs/UI) — those land with their own tests in their own tasks per `plan.md`.

---

## 3. Test pyramid we adopt

| Layer | Backend | Frontend |
|---|---|---|
| **Unit** | Services, repositories, middleware, DTOs | Pure functions, hooks, components in isolation |
| **Integration** | Controllers via `WebApplicationFactory` + InMemory DB | Components with MSW-stubbed API |
| **E2E** | _deferred_ | _deferred_ |

Rule: prefer the lowest level that captures the behavior. A controller logic test goes in **integration** only if it crosses the HTTP boundary; otherwise a unit test of the service it delegates to is enough.

---

## 4. Tooling

### Backend
- **xUnit** — test framework
- **FluentAssertions** — readable assertions (`result.Should().Be(...)`)
- **Microsoft.AspNetCore.Mvc.Testing** — `WebApplicationFactory<Program>` for in-process HTTP tests
- **Microsoft.EntityFrameworkCore.InMemory** — `UseInMemoryDatabase` for tests (decision: in-memory over Testcontainers, see §6)
- **coverlet.collector** — coverage (default in `dotnet test`)

### Frontend
- **Vitest** — test runner (Vite-native, fast, Jest-compatible API)
- **@testing-library/react** + **@testing-library/user-event** — component testing
- **@testing-library/jest-dom** — DOM matchers
- **jsdom** — browser-like environment
- **MSW (Mock Service Worker)** — stub network calls in component tests

---

## 5. Test workflow (TDD discipline)

For every behavior change:

1. **Red** — write a failing test that names the behavior in the test name.
   - `MethodName_StateUnderTest_ExpectedBehavior`
   - Example: `Register_WithDuplicateEmail_ReturnsBadRequest`
2. **Green** — write the **minimum** production code to make it pass. No extra features.
3. **Refactor** — clean up duplication, improve naming. Tests stay green.
4. **Run the whole suite** before committing. `npm test` + `dotnet test` must both be green.

Forbidden:
- "I'll add tests later" — never merge behavior without a test.
- Refactoring production code in a "test only" commit — the test is a forcing function for clean design.
- Asserting on `DateTime.Now` / `Guid.NewGuid()` directly — inject clocks and `IGuidGenerator` if you need determinism.

---

## 6. Database choice for backend tests: InMemory

**Decision:** EF Core InMemory provider.

**Why:**
- Zero infra: no Docker, no port conflicts, no cleanup.
- Deterministic and fast (~50 ms per test class spin-up vs ~5 s for Testcontainers).
- Sufficient for the current scope (controllers + services + middleware).

**Known limitations and how we handle them:**
- No relational constraints are enforced. Tests that rely on FK cascade behavior must use a real DB.
- `SaveChanges` does not validate unique indexes. Tests for "duplicate email rejected" assert on the application result (Identity error), not a DB exception.
- Transactions across connections don't roll back. Tests use **per-test fresh DB** (`Guid`-named DB) instead of shared state + rollback.

When we need real SQL (e.g., raw migrations, Postgres-specific features), we add an opt-in `[Trait("Category", "Integration-DB")]` test that uses Testcontainers, behind a feature flag.

---

## 7. JWT configuration in tests

`Program.cs` fails fast if `Jwt:Key` is missing or shorter than 32 bytes. Our test host pre-configures a known 64-char key in `WebApplicationFactory<Program>.ConfigureWebHost` before app build. We never read the real production key in tests.

---

## 8. Conventions

### File & folder layout

**Backend**
```
backend/
  MiniEcommerce.Api.Tests/
    MiniEcommerce.Api.Tests.csproj
    Infrastructure/
      ApiFactory.cs                # WebApplicationFactory<Program>
      ApiFactoryCollection.cs      # xUnit collection fixture
      InMemoryDbContextFactory.cs  # per-test fresh DB
    Unit/
      Repositories/
        RepositoryTests.cs
      Middleware/
        ExceptionMiddlewareTests.cs
    Integration/
      Controllers/
        AuthControllerTests.cs
        HealthControllerTests.cs
```

**Frontend**
```
frontend/
  src/
    test/
      setup.ts
      server.ts                    # MSW server
      handlers.ts                  # default MSW handlers
    lib/
      utils.test.ts                # colocated unit test
    App.test.tsx                   # colocated smoke test
```

### Naming
- Test class = class under test + `Tests` suffix: `RepositoryTests`, `AuthControllerTests`.
- Test method = `Method_State_Expectation`.
- One assertion concept per test (multiple `.Should()` calls on the same object are fine).

### What we do NOT do
- Do not test private methods directly.
- Do not assert on log messages (brittle).
- Do not use `Thread.Sleep` — use polling or fakes.
- Do not share state between tests (no static mutable fields).

---

## 9. CI

`.github/workflows/ci.yml` runs on every push and PR:
1. `dotnet restore && dotnet test` — backend
2. `npm ci && npm run lint && npm test -- --run` — frontend

Both must pass to merge.

---

## 10. Out-of-scope decisions (recorded for later)

- Playwright e2e — separate task when features stabilize.
- Mutation testing (Stryker) — interesting, but slow and noisy. Revisit after coverage stabilizes.
- Performance/load tests — separate task when we have a staging environment.
