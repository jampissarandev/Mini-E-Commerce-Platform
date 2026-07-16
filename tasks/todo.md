# todo.md

> Granular task list derived from `tasks/plan.md`. Sub-tasks use the parent number + letter (e.g., `1a` is part of Task 1). Each sub-task is S or M scoped (≤5 files, single subsystem).
>
> **Source of truth:** `plan.md` owns the architecture, ADRs, and acceptance criteria. This file is a working mirror of the actionable checklist and is re-synced from `plan.md`. When they disagree, `plan.md` wins.
>
> **Phase 7 — Deferred ADRs** (atomic stock, token refresh, address book, variants, payment failure modes) lives at the bottom of this file as **Tasks 24–28**. Tasks 21–25 in `plan.md` are the same work under the historical numbering; the renumber keeps the Phase 6 checklist (Tasks 18–23) stable.

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

- [x] **Task 8: Product Catalog UI**
  - [x] 8a: Product card component + grid
  - [x] 8b: Category filter + search bar (URL-driven)
  - [x] 8c: Pagination component
  - [x] 8d: Product list page
  - [x] 8e: Product detail page
  - [x] 8f: TanStack Query hooks for products

## Checkpoint: Catalog
- [x] `/products` shows all seeded products with images
- [x] Search and category filter work
- [x] Pagination updates URL and fetches correctly
- [x] `/products/:id` shows product detail
- [x] Skeleton loaders and empty states render

---

## Phase 4: Cart & Checkout

- [x] **Task 9: Cart API Endpoints**
  - [x] 9a: GET /cart
  - [x] 9b: POST /cart/items + PUT /cart/items/:id + DELETE /cart/items/:id
  - [x] 9c: DELETE /cart (clear)

- [x] **Task 10: Cart UI**
  - [x] 10a: Cart store (TanStack Query) + hook
  - [x] 10b: Cart icon with item count badge
  - [x] 10c: Cart sheet (shadcn Sheet)
  - [x] 10d: Add-to-cart from product pages

- [x] **Task 11: Checkout API**
  - [x] 11a: POST /orders (with stock re-validation, payment, stock deduction)
  - [x] 11b: GET /orders + GET /orders/:id

- [x] **Task 12: Checkout UI**
  - [x] 12a: Checkout form (shipping)
  - [x] 12b: Order confirmation page
  - [x] 12c: Order history page

## Checkpoint: Cart & Checkout
- [x] Customer can add items to cart from list and detail pages
- [x] Cart sheet shows live updates
- [x] Checkout creates an order, decrements stock, clears cart
- [x] Order confirmation + history pages work
- [x] Payment failure (mock) handled gracefully

---

## Phase 5: Admin Panel

- [x] **Task 13: Admin — Product Management API**
  - [x] 13a: GET /admin/products + POST /admin/products
  - [x] 13b: PUT /admin/products/:id + DELETE /admin/products/:id
  - [x] 13c: POST /admin/products/:id/images + DELETE /admin/products/:id/images/:imageId

- [x] **Task 14: Admin — Product Management UI**
  - [x] 14a: Product data table
  - [x] 14b: Add/Edit product form with image upload
  - [x] 14c: Delete confirmation dialog

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
  - [ ] 18b: Scaffold `MiniEcommerce.Api.Tests` xUnit project + wire `dotnet test`
  - [ ] 18c: `WebApplicationFactory<Program>` test host with EF Core InMemory
  - [ ] 18d: TDD `Repository<T>` tests against InMemory DB
  - [ ] 18e: TDD `ExceptionMiddleware` tests for all exception→status mappings
  - [ ] 18f: TDD `AuthController` integration tests (register, login, /me, role gating)

- [ ] **Task 19: Frontend Testing (TDD foundation)**
  - [ ] 19a: Install Vitest + RTL + jsdom + MSW; add `test` / `coverage` scripts
  - [ ] 19b: `setup.ts` + MSW server + default handlers
  - [ ] 19c: TDD `utils.test.ts` for the `cn()` helper
  - [ ] 19d: Smoke `App.test.tsx` with MSW-stubbed `/api/health`

- [ ] **Task 20: Testing documentation & CI**
  - [ ] 20a: Write `docs/testing.md` and update `README.md`
  - [ ] 20b: Add `.github/workflows/ci.yml` running `dotnet test` + `npm test`
  - [ ] 20c: Run full suite, confirm both pass

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

---

## Phase 7: Deferred ADRs

> Implementation of the ADRs that were out-of-scope for the v1 delivery but are tracked here as Tasks 24–28 (historically Tasks 21–25 in `plan.md`). Each sub-task is S or M sized so a single agent can implement, test, and verify in a focused session. The ADR is the source of truth for the design.
>
> **Status legend:** ✅ Shipped — code lives in `backend/MiniEcommerce.Api/` and/or `frontend/src/`; 🟡 In progress; ⚪ Not started.

- [ ] **Task 24: Apply atomic stock deduction (ADR 0002)** ⚪ Not started (rolled back 2026-07-13)
  > Replaces the in-memory re-validate-then-deduct loop in 11a with the atomic SQL UPDATE pattern. On payment failure, the restock path returns `400 PAYMENT_FAILED` (NOT `402`; see `CONTEXT.md` → Payment → Payment failure).
  >
  > **Status:** The 2026-07-13 grilling caught that this task was marked ✅ Shipped in an earlier pass without verifying the code. `OrdersController.Checkout` still uses the in-memory loop (`item.Product.Stock -= item.Quantity` before `SaveChanges`). No `ExecuteSqlInterpolatedAsync` / `ExecuteSqlRaw` exists in the codebase. ADR 0002 is the target, not the implementation. v2 (Phase 8, ADR 0007) replaces the in-memory loop with reservations, which closes the race ADR 0002 was trying to close. See `docs/adr/0002-atomic-stock-deduction.md` Status section and the Risk Register row in `plan.md`.
  - [ ] 24a: Add `ExecuteSqlInterpolatedAsync` per cart item; check `rowsAffected`; return 400 `INSUFFICIENT_STOCK` on 0.
  - [ ] 24b: Add the restock loop: if `IPaymentService.ChargeAsync` returns `Success = false`, atomic-UPDATE each cart item back to the original quantity before returning `400 PAYMENT_FAILED`. (The implicit "no `SaveChanges` until payment succeeds" pattern in the current controller is a partial implementation; the explicit restock loop is the v1 target.)
  - [ ] 24c: Remove the now-redundant in-memory re-validate-then-deduct.
  - [ ] 24d: Tests — happy path, two concurrent checkouts for the last unit (exactly one succeeds), payment fail mid-checkout (stock fully restored). At least one **concurrent** test case is required.
  **Files:** `Controllers/OrdersController.cs`, `backend/MiniEcommerce.Api.Tests/Integration/Controllers/OrdersControllerTests.cs`

- [ ] **Task 25: Add silent token refresh (ADR 0005)** ⚪ Not started
  > Add a `RefreshTokens` table and the `/api/auth/refresh` + `/api/auth/logout` endpoints. The frontend axios interceptor retries a 401 once via refresh before logging the user out.
  - [ ] 25a: Add `RefreshTokens` table (store `TokenHash`, not the token) + EF migration. (S)
  - [ ] 25b: `POST /api/auth/refresh` (httpOnly cookie in, access token out, rotation: old token `RevokedAt` + `ReplacedById` → new token). (M)
  - [ ] 25c: `POST /api/auth/logout` (revoke active refresh, clear cookie). (S)
  - [ ] 25d: Frontend axios interceptor: 401 → refresh → retry once. (M)
  - [ ] 25e: Adjust `Jwt:RefreshExpiresInDays` (default 30) in `appsettings.json`. (S)
  **Files:** `Models/RefreshToken.cs`, `Data/ApplicationDbContext.cs`, `Controllers/AuthController.cs`, `Dtos/Auth/*`, `frontend/src/lib/api.ts`, `frontend/src/lib/auth-store.ts`

- [ ] **Task 26: Add Customer Address Book (ADR 0004)** ⚪ Not started
  > A Customer has 0..n `Address` rows with one `IsDefault`. At checkout the customer picks an existing Address or types a one-off one. The chosen Address is snapshotted onto the Order as flat `Shipping*` fields.
  - [ ] 26a: Add `Addresses` table + EF migration. (S)
  - [ ] 26b: `GET /api/addresses`, `POST /api/addresses`, `PUT /api/addresses/:id`, `DELETE /api/addresses/:id`, `PUT /api/addresses/:id/default`. Setting `IsDefault = true` unsets it on others in the same transaction. (M)
  - [ ] 26c: Frontend `/account/addresses` page: list, edit, delete, set default. (M)
  - [ ] 26d: Checkout form: address picker (existing or new) + "save this address" checkbox. (M)
  - [ ] 26e: Wire `CheckoutRequest` to accept an `addressId` and snapshot the address onto the Order. (S)
  **Files:** `Models/Address.cs`, `Data/ApplicationDbContext.cs`, `Controllers/AddressesController.cs`, `Dtos/Address/*`, `frontend/src/pages/account/Addresses.tsx`, `frontend/src/pages/Checkout.tsx`

- [ ] **Task 27: Add Product Variants (ADR 0003)** ⚪ Not started
  > `Product` is a model; `ProductVariant` is the sellable unit with its own `Stock` and `Sku`. `CartItem` and `OrderItem` reference a Variant, not a Product.
  - [ ] 27a: Add `ProductVariants` table (Id, ProductId, Sku, Size?, Color?, Stock, IsActive) + EF migration. Drop `Product.Stock`. (M)
  - [ ] 27b: Update `GET /products` + `GET /products/:id` to expose variants. (M)
  - [ ] 27c: Switch `CartItem.ProductId` → `CartItem.ProductVariantId`; same for `OrderItem`. Update `OrderItem.ProductName` snapshot to include chosen `Size`/`Color`. (M)
  - [ ] 27d: Frontend `ProductCard` (group variants) + `ProductDetail` (variant picker: size, color). (M)
  - [ ] 27e: Update Task 11a's atomic UPDATE to target `ProductVariants`, not `Products`. (S)
  **Files:** `Models/ProductVariant.cs`, `Data/ApplicationDbContext.cs`, `Controllers/ProductsController.cs`, `Controllers/CartController.cs`, `Controllers/OrdersController.cs`, `Dtos/Product/*`, `frontend/src/pages/ProductDetail.tsx`, `frontend/src/components/ProductCard.tsx`

- [x] **Task 28: Add payment mock failure modes (CONTEXT.md → Payment)** ✅ Shipped 2026-07-13
  > Extend `MockPaymentService` with `AlwaysSucceed` (default) | `AlwaysFail` | `FailIfAmountGreaterThan(threshold)` so the `400 PAYMENT_FAILED` path in Task 11a is testable end-to-end. Mode is bound from `appsettings.json` (`Payments:Mock:Mode`).
  - [x] 28a: Add `Mode` enum + `FailIfAmountGreaterThan` threshold.
  - [x] 28b: Bind mode from `appsettings.json` via `IOptions<PaymentMockOptions>` (plus a live `IOptions` wrapper + holder so tests can flip the mode at runtime).
  - [x] 28c: Tests — `OrdersController` returns `400 PAYMENT_FAILED` for `AlwaysFail`; restock loop runs (per Task 24b when 24b lands). Mode is exposed to the frontend via `GET /api/payments/mock-mode` (no auth, non-sensitive) and a dev-only amber banner renders in `Checkout` when the mode ≠ `AlwaysSucceed`.
  **Files:** `Services/MockPaymentService.cs`, `Services/MockPaymentOptions.cs`, `Controllers/PaymentsController.cs`, `frontend/src/lib/usePaymentMode.ts`, `frontend/src/pages/Checkout.tsx`, `backend/MiniEcommerce.Api.Tests/Unit/Services/MockPaymentServiceTests.cs`, `backend/MiniEcommerce.Api.Tests/Integration/Controllers/OrdersControllerTests.cs`

## Checkpoint: Phase 7 (Deferred ADRs)
- [ ] ~~Concurrent checkouts for the last unit — exactly one succeeds (Task 24)~~ ⚪ Not started (rolled back 2026-07-13; see ADR 0002 Status)
- [ ] Refresh rotation works; concurrent refresh limited to one active token per customer (Task 25)
- [ ] Customer can save, edit, delete addresses; checkout uses the address book (Task 26)
- [ ] Product variants render in catalog; cart + checkout reference variants; stock is per-variant (Task 27)
- [x] ~~Payment failure mode triggers `400 PAYMENT_FAILED` + stock restore; failure path is covered by integration test (Task 28)~~ ✅ Shipped
- [ ] `docs/adr/*` and `CONTEXT.md` unchanged after each task lands (ADRs are the source of truth, not the implementation)

## Phase 8: v2 Rewrite — Event-sourced Orders + Reservation-based Stock

> Implementation of ADR 0006 (event-sourced orders) and ADR 0007 (reservation-based stock). v1 ships with the in-memory loop and mutate-in-place `Order`; v2 replaces both. **Out of scope for v1.** Each sub-task is L or XL sized (multiple subsystems) — break into smaller sub-tasks at the start of Phase 8.
>
> **Status legend (as of 2026-07-13):** ⚪ Not started. Phase 8 starts only when the team has decided to do v2.
> In `plan.md` these are Tasks 29–30 (the historical numbering for the ADRs that cite them).

- [ ] **Task 29: Event-sourced orders (ADR 0006)** ⚪ Not started
  > Replace mutate-in-place `Order.Status` with an append-only `OrderEvents` stream. The `Order` row becomes a projection, rebuilt by replaying events for an `OrderId`. Read-side `OrderItems` stays as a denormalised projection.
  - [ ] 29a: Add `OrderEvents` table (`Id, OrderId, EventType, PayloadJson, OccurredAt, ActorId?`) + EF migration. (S)
  - [ ] 29b: Add `EventType` enum: `Created, StockReserved, PaymentConfirmed, PaymentFailed, Shipped, Delivered, Cancelled, Refunded, Abandoned`. The v1 `OrderStatus` enum is replaced by `MAX(OccurredAt) → EventType` mapped to a denormalised `Status` column. (M)
  - [ ] 29c: Refactor `OrdersController.Checkout` to insert a `Created` event carrying the full snapshot (items, shipping, total) in a single transaction. (M)
  - [ ] 29d: Refactor `OrdersController.GetOrders` / `GetOrderById` to read the projection (no change to the public response shape). Add `?includeEvents=true` for admin tooling. (S)
  - [ ] 29e: Add `OrderCancelled` event on admin status flip (Task 15c) and `OrderRefunded` event on refund (future, requires a real payment provider). (M)
  - [ ] 29f: Tests — (a) replay produces identical `Order` row; (b) two events on the same order linearise by `OccurredAt`; (c) the `Status` denormalised column stays consistent with the latest event. (M)
  - [ ] 29g: Update ADR 0006 to record the v1 → v2 migration story. (S)
  **Files:** `Models/OrderEvent.cs`, `Data/ApplicationDbContext.cs`, `Controllers/OrdersController.cs`, `Controllers/AdminOrdersController.cs`, `Services/OrderEventService.cs`, `backend/MiniEcommerce.Api.Tests/Integration/Controllers/OrdersControllerTests.cs`

- [ ] **Task 30: Reservation-based stock (ADR 0007)** ⚪ Not started
  > Replace the in-memory `Stock -= qty` with an `OrderReservations` table. Pre-payment flow inserts reservation rows in `Held` state; payment success converts them to `Confirmed` and deducts `ProductVariant.Stock`; payment failure or abandonment releases the reservation.
  - [ ] 30a: Add `OrderReservations` table (`Id, OrderId, ProductVariantId, ProductId (v1 fallback), Quantity, Status (Held/Confirmed/Released/Expired), ExpiresAt, CreatedAt`) with index on `(Status, ExpiresAt)`. (S)
  - [ ] 30b: Refactor `OrdersController.Checkout` to: (i) check available stock (`Stock − SUM(Held reservations for this product) ≥ cart qty`); (ii) insert reservations in `Held` state; (iii) call `IPaymentService.ChargeAsync`; (iv) on success, `Confirmed` + deduct `Stock` + emit `PaymentConfirmed` event; (v) on failure, `Released` + no `Stock` change. (L)
  - [ ] 30c: Add the TTL-sweep background job (`IHostedService`) that scans `OrderReservations` for `Status = Held AND ExpiresAt < UtcNow`, marks them `Expired`, releases the reservation, and emits an `Abandoned` event. (M)
  - [ ] 30d: Update admin status flip to emit a `Cancelled` event which releases any remaining `Held` reservations. (S)
  - [ ] 30e: Tests — (a) happy path; (b) two concurrent checkouts for the last unit — exactly one wins, the other gets `400 INSUFFICIENT_STOCK`; (c) payment failure mid-checkout — reservations released, no `Stock` change; (d) abandonment — TTL sweep releases reservations and emits `Abandoned`. (L)
  - [ ] 30f: Mark ADR 0002 as superseded (it was the v1 stopgap; reservations are the v2 truth). (S)
  **Files:** `Models/OrderReservation.cs`, `Data/ApplicationDbContext.cs`, `Controllers/OrdersController.cs`, `Services/OrderReservationService.cs`, `Services/ReservationSweepService.cs` (background job), `backend/MiniEcommerce.Api.Tests/Integration/Controllers/OrdersControllerTests.cs`

## Checkpoint: Phase 8 (v2 Rewrite)
- [ ] Append-only `OrderEvents` stream is the source of truth; `Order` row is a projection (Task 29)
- [ ] Stock is reserved, not deducted, at checkout (Task 30)
- [ ] TTL-sweep background job releases unconfirmed reservations after N minutes (Task 30c)
- [ ] `Abandoned` is a first-class event, not a missing transition (Tasks 29b + 30c)
- [ ] Concurrent-checkout integration test exists and passes for the last unit (Task 30e)
- [ ] ADR 0002 is marked superseded; ADR 0006 + 0007 are the canonical answer
- [ ] `docs/adr/*` and `CONTEXT.md` updated to reflect the v2 model; the v1 model is the historical record, not the source of truth
