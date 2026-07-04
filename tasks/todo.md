# todo.md

> Granular task list derived from `tasks/plan.md`. Sub-tasks use the parent number + letter (e.g., `1a` is part of Task 1). Each sub-task is S or M scoped (≤5 files, single subsystem).

## Phase 1: Foundation

- [x] **Task 1: Initialize Backend Project & Docker**
  - [x] 1a: Scaffold ASP.NET Core Web API project
  - [x] 1b: Add NuGet packages (EF Core, Npgsql, Identity, JWT, ImageSharp 3.1.x)
  - [x] 1c: Add Dockerfile + docker-compose for API and PostgreSQL
  - [x] 1d: Configure appsettings + health endpoint + Swagger

- [x] **Task 2: Initialize Frontend Project**
  - [x] 2a: Scaffold React + Vite + TypeScript
  - [x] 2b: Install + configure Tailwind CSS + shadcn/ui
  - [x] 2c: Install TanStack Query, Zustand, React Router, Axios + Vite proxy
  - [x] 2d: Add base layout + route shell (Layout, Navbar, 404)

- [x] **Task 3: Database Schema & Migrations**
  - [x] 3a: Define User (Identity) + Role entities
  - [x] 3b: Define Category + Product + ProductImage entities
  - [x] 3c: Define Cart + CartItem entities
  - [x] 3d: Define Order + OrderItem entities
  - [x] 3e: Apply migrations + verify schema
  - [x] 3f: Add seed data (admin user, categories, products)

- [x] **Task 4: Base Architecture & Utilities**
  - [x] 4a: Generic repository pattern (IRepository<T>, Repository<T>)
  - [x] 4b: API response wrapper + exception middleware
  - [x] 4c: IImageStorage interface + LocalImageStorage
  - [x] 4d: IPaymentService interface + MockPaymentService
  - [x] 4e: Service registration + DI setup (AddApplicationServices)

## Checkpoint: Foundation
- [x] `docker-compose up` brings up API + PostgreSQL
- [x] `curl http://localhost:5000/health` returns 200
- [x] `curl http://localhost:5000/swagger` lists endpoints
- [x] Frontend dev server starts with Tailwind + shadcn button working
- [x] All migrations applied, seed data present in DB
- [x] `/api/health` reachable via Vite proxy

---

## Phase 2: Authentication & Identity

- [x] **Task 5: Identity & Auth Backend**
  - [x] 5a: Configure Identity + JWT in Program.cs
  - [x] 5b: Implement /auth/register endpoint
  - [x] 5c: Implement /auth/login endpoint
  - [x] 5d: Add /auth/me endpoint + role attributes

- [x] **Task 6: Auth Frontend**
  - [x] 6a: Zustand auth store (with persist)
  - [x] 6b: Axios instance with auth interceptor
  - [x] 6c: Login + Register pages with shadcn forms
  - [x] 6d: Protected + role-based route guards
  - [x] 6e: Navbar with auth state

## Checkpoint: Auth
- [x] User can register and login via UI
- [x] Backend issues JWT with role claim
- [x] Customer cannot access `/admin` (403)
- [x] Admin can access `/admin` (200)
- [x] Token persists across reload
- [x] 401 from API triggers logout + redirect

---

## Phase 3: Product Catalog (Customer)

- [ ] **Task 7: Product API Endpoints**
  - [ ] 7a: GET /products (pagination, filtering, sorting)
  - [ ] 7b: GET /products/:id
  - [ ] 7c: GET /categories

- [ ] **Task 8: Product Catalog UI**
  - [ ] 8a: Product card component + grid
  - [ ] 8b: Category filter + search bar (URL-driven)
  - [ ] 8c: Pagination component
  - [ ] 8d: Product list page
  - [ ] 8e: Product detail page
  - [ ] 8f: TanStack Query hooks for products

## Checkpoint: Catalog
- [ ] `/products` shows all seeded products with images
- [ ] Search and category filter work
- [ ] Pagination updates URL and fetches correctly
- [ ] `/products/:slug` shows product detail
- [ ] Skeleton loaders and empty states render

---

## Phase 4: Cart & Checkout

- [ ] **Task 9: Cart API Endpoints**
  - [ ] 9a: GET /cart
  - [ ] 9b: POST /cart/items + PUT /cart/items/:id + DELETE /cart/items/:id
  - [ ] 9c: DELETE /cart (clear)

- [ ] **Task 10: Cart UI**
  - [ ] 10a: Cart store (TanStack Query) + hook
  - [ ] 10b: Cart icon with item count badge
  - [ ] 10c: Cart sheet (shadcn Sheet)
  - [ ] 10d: Add-to-cart from product pages

- [ ] **Task 11: Checkout API**
  - [ ] 11a: POST /orders (with stock re-validation, payment, stock deduction)
  - [ ] 11b: GET /orders + GET /orders/:id

- [ ] **Task 12: Checkout UI**
  - [ ] 12a: Checkout form (shipping)
  - [ ] 12b: Order confirmation page
  - [ ] 12c: Order history page

## Checkpoint: Cart & Checkout
- [ ] Customer can add items to cart from list and detail pages
- [ ] Cart sheet shows live updates
- [ ] Checkout creates an order, decrements stock, clears cart
- [ ] Order confirmation + history pages work
- [ ] Payment failure (mock) handled gracefully

---

## Phase 5: Admin Panel

- [ ] **Task 13: Admin — Product Management API**
  - [ ] 13a: GET /admin/products + POST /admin/products
  - [ ] 13b: PUT /admin/products/:id + DELETE /admin/products/:id
  - [ ] 13c: POST /admin/products/:id/images + DELETE /admin/products/:id/images/:imageId

- [ ] **Task 14: Admin — Product Management UI**
  - [ ] 14a: Product data table
  - [ ] 14b: Add/Edit product form with image upload
  - [ ] 14c: Delete confirmation dialog

- [ ] **Task 15: Admin — Order Management API**
  - [ ] 15a: GET /admin/orders (with filters)
  - [ ] 15b: GET /admin/orders/:id
  - [ ] 15c: PUT /admin/orders/:id/status (with transition guard rails)

- [ ] **Task 16: Admin — Order Management UI**
  - [ ] 16a: Orders table with status filter
  - [ ] 16b: Order detail view
  - [ ] 16c: Status update dropdown

- [ ] **Task 17: Admin — Dashboard**
  - [ ] 17a: Dashboard stats endpoints
  - [ ] 17b: Dashboard cards UI
  - [ ] 17c: Charts (sales line + recent orders list + low stock table)

## Checkpoint: Admin
- [ ] Admin can CRUD products, upload images
- [ ] Admin can list orders, view detail, update status
- [ ] Cancelling an order restocks items
- [ ] Dashboard shows summary, sales chart, recent orders, low stock

---

## Phase 6: Testing & Polish

- [ ] **Task 18: Backend Testing (TDD foundation)**
  - [x] 18a: Write `tasks/test-spec.md` (test strategy, conventions, DB choice)
  - [ ] 18b: Scaffold `MiniEcommerce.Api.Tests` xUnit project + wire `dotnet test` (21a)
  - [ ] 18c: `WebApplicationFactory<Program>` test host with EF Core InMemory (21b)
  - [ ] 18d: TDD `Repository<T>` tests against InMemory DB (21c)
  - [ ] 18e: TDD `ExceptionMiddleware` tests for all exception→status mappings (21d)
  - [ ] 18f: TDD `AuthController` integration tests (register, login, /me, role gating) (21e)

- [ ] **Task 19: Frontend Testing (TDD foundation)**
  - [ ] 19a: Install Vitest + RTL + jsdom + MSW; add `test` / `coverage` scripts (21f)
  - [ ] 19b: `setup.ts` + MSW server + default handlers (21g)
  - [ ] 19c: TDD `utils.test.ts` for the `cn()` helper (21h)
  - [ ] 19d: Smoke `App.test.tsx` with MSW-stubbed `/api/health` (21i)

- [ ] **Task 20: Testing documentation & CI**
  - [ ] 20a: Write `docs/testing.md` and update `README.md` (21j)
  - [ ] 20b: Add `.github/workflows/ci.yml` running `dotnet test` + `npm test` (21k)
  - [ ] 20c: Run full suite, confirm both pass (21l)

- [ ] **Task 21: Documentation**
  - [ ] 21a: README + setup instructions
  - [ ] 21b: Swagger annotations
  - [ ] 21c: VPS deployment guide

- [ ] **Task 22: Docker Production Build**
  - [ ] 22a: Multi-stage Dockerfile for API
  - [ ] 22b: Multi-stage Dockerfile for frontend (Nginx)
  - [ ] 22c: Production docker-compose + env config

- [x] **Task 23: Dependency Upgrades (latest)**
  > Snapshot 2026-07-04. Strategy: see [plan.md "Dependency Upgrade Strategy"](plan.md#dependency-upgrade-strategy). One major per PR. Tests gate every upgrade.
  - [ ] 23a: ImageSharp 3.1→4.0 — **BLOCKED**: v4.0.0 (released 2026-05-12) is a commercial product; build target requires a `SixLaborsLicenseKey` / `sixlabors.lic` file. Decision needed: keep 3.1.* (latest MIT/free) or purchase a commercial license.
  - [x] 23b: Swashbuckle.AspNetCore 6.9→10.2 (commit `b9c187d`; `Program.cs` migrated: `Microsoft.OpenApi.Models`→`Microsoft.OpenApi`, `OpenApiReference`→`OpenApiSecuritySchemeReference`, `AddSecurityRequirement` now `Func<OpenApiDocument, OpenApiSecurityRequirement>` with `List<string>` scopes; 27/27 backend tests pass, 0 vulnerabilities)
  - [x] 23c: Vite 6→8 + @vitejs/plugin-react 4→6 (commit `eacb41c`; no config changes required; 55/55 frontend tests pass, production build clean)
  - [x] 23d: ESLint 9→10 (commit `d80e31e`; flat config forward-compatible; lint clean)
  - [x] 23e: @types/node 22→26 (commit `6765bdf`; type-only upgrade; `tsc -b` clean)
  - [x] 23f: Security audit (2026-07-04) — `npm audit`: **0 vulnerabilities**; `dotnet list package --vulnerable --include-transitive` on `MiniEcommerce.Api` and `MiniEcommerce.Api.Tests`: **0 vulnerable packages**. Branch `chore/deps-2026-07-04` ready to merge.

## Checkpoint: Complete
- [ ] `dotnet test` returns 0
- [ ] `npm test` returns 0
- [ ] `npm run build` returns 0
- [ ] `docker compose -f docker-compose.prod.yml up` brings up the full stack
- [ ] Customer and admin flows tested end-to-end
- [ ] README + Swagger cover the full surface
- [ ] Every future behavior change has a test written first (TDD)
