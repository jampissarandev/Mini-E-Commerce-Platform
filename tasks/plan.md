# Implementation Plan: Mini E-Commerce Platform

## Overview
A full-stack e-commerce application with customer-facing shopping and admin management. Built with React + TanStack Query + Zustand (frontend), ASP.NET Core Web API (backend), and PostgreSQL (database). Features include user auth, product catalog, shopping cart, checkout, and admin dashboard.

---

## Architecture Decisions

- **REST API** — Simpler with ASP.NET Core, sufficient for this scope
- **JWT Authentication** — Stateless, works well with React + API separation
- **Route-based Role Guards** — Single frontend build, role-based access control on routes
- **Strategy Pattern for Extensibility** — Image storage and payment services use strategy pattern for easy swapping (local→cloud, mock→Stripe)
- **Database-First via EF Core Migrations** — Standard ASP.NET Core approach
- **Docker + docker-compose** — Local development and VPS deployment ready
- **Tailwind CSS + shadcn/ui** — Modern, customizable UI components

---

## Extensibility Points (Future-Proofing)

| Component | Current | Future |
|-----------|---------|--------|
| Image Storage | Local file system via `IImageStorage` interface | Cloudinary, AWS S3 |
| Payment | Mock service via `IPaymentService` interface | Stripe, PayPal |
| Email | No-op / placeholder | SendGrid, Mailgun integration |

---

## Dependency Graph

```
PostgreSQL (Docker)
    │
    ├── EF Core Migrations (Database Schema)
    │       │
    │       ├── ASP.NET Core Identity (Users, Roles)
    │       │       │
    │       │       ├── JWT Auth (API)
    │       │       │       │
    │       │       │       ├── Product Catalog API
    │       │       │       └── Cart & Checkout API
    │       │       │               │
    │       │       │               ├── Admin API (Products, Orders)
    │       │       │
    │       │       └── React Frontend
    │               │
    │               ├── Customer: Product Catalog, Cart, Checkout
    │               └── Admin: Product/Order Management, Dashboard
    │
    └── Seed Data (Categories, Products, Admin User)
```

---

## Sub-task Sizing & Breakdown Rationale

The original 20 tasks are L or XL in scope (5–10+ files, multiple subsystems). Per the planning skill, every sub-task must be S or M sized (≤5 files, single subsystem) so an agent can implement, test, and verify in a single focused session. Each parent task (1, 2, 3 …) is broken into 2–5 sub-tasks labeled `Nx` (e.g., `1a`, `1b`). Acceptance criteria, verification, dependencies, files, and scope are explicit for every sub-task.

**Sizing legend:** `S` = 1–2 files · `M` = 3–5 files · `L` = 5–8 files · `XL` = 8+ files (never used — always broken down further).

---

## Parallelization Matrix

Once the foundation (Phase 1) and the API contracts of Phase 2 (auth DTOs, register/login responses) are stable, the following streams can run in parallel across agents or sessions:

| Stream | Sub-tasks | Coordination needed |
|---|---|---|
| Backend — Catalog | 7a, 7b, 7c | None (independent of cart/checkout) |
| Frontend — Catalog | 8a–8f (after 7a is merged) | Needs final response shape of `GET /products` |
| Backend — Cart | 9a, 9b, 9c | Needs `Product` schema from Phase 1 |
| Frontend — Cart | 10a–10d (after 9b is merged) | Needs `CartItem` response shape |
| Backend — Checkout | 11a, 11b | Sequential after Cart |
| Frontend — Checkout | 12a–12d (after 11a is merged) | Needs `Order` response shape |
| Backend — Admin Product | 13a–13c | Sequential after Catalog backend |
| Frontend — Admin Product | 14a–14c | After 13a–13c merge |
| Backend — Admin Order | 15a–15c | After Checkout backend |
| Frontend — Admin Order | 16a–16c | After 15a–15c merge |
| Backend — Dashboard | 17a | After Orders exist |
| Frontend — Dashboard | 17b, 17c | After 17a merges |
| Tests / Docs / Docker | 18*, 19*, 20* | Independent of feature work once features land |

**Rule:** never run two sub-tasks in parallel if they both edit the same backend `DbContext`, the same controller, or the same frontend page. Define the contract first, then fan out.

---

## Task List

### Phase 1: Foundation

#### Task 1: Initialize Backend Project & Docker

##### Task 1a: Scaffold ASP.NET Core Web API project
**Description:** Create the solution structure for the backend using Clean Architecture folders (Controllers, Services, Repositories, Models, DTOs, Data). Use a single Web API project for the learning scope; layers are organized by folder.

**Acceptance criteria:**
- [ ] `dotnet new webapi` project created at `backend/MiniEcommerce.Api/`
- [ ] Folders created: `Controllers/`, `Services/`, `Repositories/`, `Models/`, `Dtos/`, `Data/`, `Interfaces/`
- [ ] Default `WeatherForecast` controller removed
- [ ] `dotnet build` succeeds with 0 warnings (treat-warnings-as-errors enabled)

**Verification:**
- [ ] Build: `dotnet build` returns 0
- [ ] Manual: `dotnet run` starts API on `https://localhost:5001`

**Dependencies:** None
**Files likely touched:** `backend/MiniEcommerce.Api.csproj`, `Program.cs`, `appsettings.json`, new folder structure
**Estimated scope:** S

---

##### Task 1b: Add NuGet packages
**Description:** Add the core packages needed for EF Core + PostgreSQL, Identity, JWT, Swagger, and image handling.

**Acceptance criteria:**
- [ ] `Microsoft.EntityFrameworkCore` + `Npgsql.EntityFrameworkCore.PostgreSQL` installed
- [ ] `Microsoft.AspNetCore.Identity.EntityFrameworkCore` installed
- [ ] `Microsoft.AspNetCore.Authentication.JwtBearer` installed
- [ ] `Swashbuckle.AspNetCore` installed
- [ ] `SixLabors.ImageSharp` installed for image validation
- [ ] `dotnet list package` shows all packages at stable versions

**Verification:**
- [ ] Build: `dotnet restore && dotnet build` returns 0

**Dependencies:** 1a
**Files likely touched:** `backend/MiniEcommerce.Api.csproj`
**Estimated scope:** S

---

##### Task 1c: Add Dockerfile + docker-compose for API and PostgreSQL
**Description:** Containerize the API for development and set up PostgreSQL via docker-compose with a persistent volume.

**Acceptance criteria:**
- [ ] `backend/Dockerfile` (multi-stage: sdk → aspnet) created
- [ ] `docker-compose.yml` at repo root with `api` and `db` services
- [ ] PostgreSQL service uses volume `pgdata` and exposes `5432`
- [ ] API service depends on `db` healthcheck, exposes `5000`
- [ ] `docker-compose config` validates without errors

**Verification:**
- [ ] `docker-compose up -d --build` starts both containers
- [ ] `docker-compose ps` shows both services healthy
- [ ] `docker-compose logs api` shows ASP.NET Core startup banner

**Dependencies:** 1a
**Files likely touched:** `backend/Dockerfile`, `docker-compose.yml`, `backend/.dockerignore`
**Estimated scope:** M

---

##### Task 1d: Configure appsettings + health endpoint
**Description:** Wire up configuration for PostgreSQL connection string, JWT settings, and static files. Add a health endpoint and enable Swagger in development.

**Acceptance criteria:**
- [ ] `appsettings.json` contains `ConnectionStrings:Default`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key`
- [ ] `appsettings.Development.json` overrides connection string for docker
- [ ] `GET /health` returns 200 `{"status":"ok"}`
- [ ] Swagger UI available at `/swagger` in development
- [ ] Static files served from `wwwroot/`

**Verification:**
- [ ] `curl http://localhost:5000/health` returns 200
- [ ] Browser opens `http://localhost:5000/swagger` and shows empty API list

**Dependencies:** 1b, 1c
**Files likely touched:** `appsettings.json`, `appsettings.Development.json`, `Program.cs`, `Controllers/HealthController.cs`
**Estimated scope:** S

---

#### Task 2: Initialize Frontend Project

##### Task 2a: Scaffold React + Vite + TypeScript
**Description:** Create the frontend project with Vite, React 18, TypeScript strict mode, and ESLint/Prettier configured to match the repo's conventions.

**Acceptance criteria:**
- [ ] Vite project created at `frontend/` with React + TS template
- [ ] `tsconfig.json` has `"strict": true`
- [ ] `npm run dev` starts on `http://localhost:5173`
- [ ] `npm run build` produces `dist/`
- [ ] Default Vite styles removed, blank `<App />` renders

**Verification:**
- [ ] `npm run build` returns 0
- [ ] Browser shows empty page with no console errors

**Dependencies:** None
**Files likely touched:** `frontend/package.json`, `frontend/vite.config.ts`, `frontend/tsconfig.json`, `frontend/src/App.tsx`
**Estimated scope:** S

---

##### Task 2b: Install + configure Tailwind CSS + shadcn/ui
**Description:** Set up Tailwind with shadcn/ui defaults (CSS variables theme, neutral palette, dark mode ready).

**Acceptance criteria:**
- [ ] Tailwind + PostCSS + Autoprefixer installed
- [ ] `tailwind.config.ts` and `postcss.config.js` configured
- [ ] `npx shadcn@latest init` succeeds with default theme
- [ ] `npx shadcn@latest add button` installs `<Button />` and a test page renders a button

**Verification:**
- [ ] Button renders with correct rounded/hover styles
- [ ] Dark mode toggle works (CSS class on `<html>`)

**Dependencies:** 2a
**Files likely touched:** `frontend/tailwind.config.ts`, `frontend/postcss.config.js`, `frontend/src/index.css`, `frontend/components.json`, `frontend/src/components/ui/button.tsx`
**Estimated scope:** M

---

##### Task 2c: Install TanStack Query, Zustand, React Router, Axios
**Description:** Add the state, routing, and HTTP client libraries that drive the app.

**Acceptance criteria:**
- [ ] `@tanstack/react-query` installed and `<QueryClientProvider>` wraps `<App />`
- [ ] `zustand` installed (no provider needed)
- [ ] `react-router-dom` v6+ installed with `<BrowserRouter>` in `main.tsx`
- [ ] `axios` installed with a base instance pointing at `import.meta.env.VITE_API_URL`
- [ ] `dev` proxy in `vite.config.ts` forwards `/api` → `http://localhost:5000`

**Verification:**
- [ ] Dev server proxies `/api/health` to API (returns 200 via `/api/health`)
- [ ] Visiting `/` renders a placeholder route

**Dependencies:** 2a
**Files likely touched:** `frontend/package.json`, `frontend/src/main.tsx`, `frontend/src/lib/api.ts`, `frontend/vite.config.ts`
**Estimated scope:** S

---

##### Task 2d: Add base layout + route shell
**Description:** Create the shared `<AppShell />` with a placeholder `<Navbar />` and `<Outlet />` so all subsequent pages have a consistent layout.

**Acceptance criteria:**
- [ ] `<Layout />` with sticky navbar and `<Outlet />` content area
- [ ] `<Navbar />` placeholder with logo + nav links (auth-aware later in 6e)
- [ ] Routes: `/`, `/products`, `/cart`, `/checkout`, `/login`, `/register`, `/admin/*`
- [ ] 404 route renders `<NotFound />`

**Verification:**
- [ ] `npm run dev` shows navbar on every route
- [ ] Navigating to `/does-not-exist` shows 404 page

**Dependencies:** 2b, 2c
**Files likely touched:** `frontend/src/components/Layout.tsx`, `frontend/src/components/Navbar.tsx`, `frontend/src/App.tsx`, `frontend/src/pages/NotFound.tsx`
**Estimated scope:** M

---

#### Task 3: Database Schema & Migrations

##### Task 3a: Define User (Identity) + Role entities
**Description:** Configure ASP.NET Core Identity backed by EF Core. Extend `IdentityUser` with `FullName` and `CreatedAt`. Use `IdentityRole` for `Customer` and `Admin`.

**Acceptance criteria:**
- [ ] `ApplicationUser : IdentityUser` with `FullName`, `CreatedAt`
- [ ] `ApplicationDbContext : IdentityDbContext<ApplicationUser>` registered in DI
- [ ] Roles seeded: `Customer`, `Admin` (idempotent on startup)
- [ ] `dotnet ef migrations add InitIdentity` succeeds

**Verification:**
- [ ] Migration file generated under `Data/Migrations/`
- [ ] `dotnet ef database update` creates `AspNet*` tables in PostgreSQL

**Dependencies:** 1d
**Files likely touched:** `Models/ApplicationUser.cs`, `Data/ApplicationDbContext.cs`, `Data/Seed.cs`, `Program.cs`
**Estimated scope:** M

---

##### Task 3b: Define Category + Product + ProductImage entities
**Description:** Add catalog entities. `Product` has many `ProductImage` rows (ordered by `SortOrder`). `Category` is a self-referencing tree (optional `ParentCategoryId`).

**Acceptance criteria:**
- [ ] `Category { Id, Name, Slug, ParentCategoryId? }` with unique slug index
- [ ] `Product { Id, Name, Slug, Description, Price, Stock, CategoryId, IsActive, CreatedAt }`
- [ ] `ProductImage { Id, ProductId, Url, SortOrder }` with cascade delete
- [ ] Fluent API config in `OnModelCreating` for indexes and relationships
- [ ] `dotnet ef migrations add Catalog` succeeds

**Verification:**
- [ ] Migration adds `Categories`, `Products`, `ProductImages` tables with FKs
- [ ] `dotnet ef database update` applies cleanly

**Dependencies:** 3a
**Files likely touched:** `Models/Category.cs`, `Models/Product.cs`, `Models/ProductImage.cs`, `Data/ApplicationDbContext.cs`
**Estimated scope:** M

---

##### Task 3c: Define Cart + CartItem entities
**Description:** Persistent cart tied to a user (one cart per user). `CartItem` references a product with quantity and a unique constraint on `(CartId, ProductId)`.

**Acceptance criteria:**
- [ ] `Cart { Id, UserId, CreatedAt, UpdatedAt }` with unique index on `UserId`
- [ ] `CartItem { Id, CartId, ProductId, Quantity, UnitPrice }` with unique `(CartId, ProductId)`
- [ ] FK to `Product` with `Restrict` delete (can't delete a product that's in any cart)
- [ ] `dotnet ef migrations add Cart` succeeds

**Verification:**
- [ ] Migration creates `Carts`, `CartItems` tables with correct FKs
- [ ] `dotnet ef database update` applies cleanly

**Dependencies:** 3b
**Files likely touched:** `Models/Cart.cs`, `Models/CartItem.cs`, `Data/ApplicationDbContext.cs`
**Estimated scope:** S

---

##### Task 3d: Define Order + OrderItem entities
**Description:** `Order` represents a completed checkout (one per transaction). `OrderItem` snapshots product name + price at purchase time.

**Acceptance criteria:**
- [ ] `Order { Id, UserId, Status, Subtotal, ShippingFee, Total, ShippingAddress, CreatedAt }`
- [ ] `OrderItem { Id, OrderId, ProductId, ProductName, UnitPrice, Quantity }`
- [ ] `OrderStatus` enum: `Pending, Paid, Shipped, Delivered, Cancelled`
- [ ] FK to `Product` with `Restrict` (preserve history)
- [ ] `dotnet ef migrations add Orders` succeeds

**Verification:**
- [ ] Migration creates `Orders`, `OrderItems` tables
- [ ] `dotnet ef database update` applies cleanly

**Dependencies:** 3c
**Files likely touched:** `Models/Order.cs`, `Models/OrderItem.cs`, `Models/OrderStatus.cs`, `Data/ApplicationDbContext.cs`
**Estimated scope:** S

---

##### Task 3e: Apply migrations + verify schema
**Description:** Apply all migrations to the running PostgreSQL container and confirm the schema matches the entity definitions.

**Acceptance criteria:**
- [ ] `dotnet ef database update` runs without error inside the API container
- [ ] All tables present: `AspNetUsers`, `AspNetRoles`, `Categories`, `Products`, `ProductImages`, `Carts`, `CartItems`, `Orders`, `OrderItems`
- [ ] `__EFMigrationsHistory` table records all applied migrations

**Verification:**
- [ ] `docker exec -it <db> psql -U postgres -d mini_ecommerce -c '\dt'` lists all tables
- [ ] Re-running `dotnet ef database update` is a no-op

**Dependencies:** 3a, 3b, 3c, 3d
**Files likely touched:** `Data/Migrations/*` (auto-generated)
**Estimated scope:** S

---

##### Task 3f: Add seed data (admin user, categories, products)
**Description:** Seed an admin user, a customer user, ~5 categories, and ~20 products with placeholder images so the app has data on first run.

**Acceptance criteria:**
- [ ] Seeded admin: `admin@example.com` / `Admin123!` (role `Admin`)
- [ ] Seeded customer: `customer@example.com` / `Customer123!` (role `Customer`)
- [ ] Seeded categories: Electronics, Books, Clothing, Home, Toys
- [ ] Seeded products: 4 per category with realistic names/prices/stock
- [ ] Idempotent: re-running does not duplicate rows

**Verification:**
- [ ] `dotnet run` logs "Seed complete: X categories, Y products, 2 users"
- [ ] DB query confirms row counts match
- [ ] `POST /auth/login` with seeded creds returns JWT (after 5b lands)

**Dependencies:** 3e
**Files likely touched:** `Data/Seed.cs`, `Program.cs`
**Estimated scope:** M

---

#### Task 4: Base Architecture & Utilities

##### Task 4a: Generic repository pattern
**Description:** Implement `IRepository<T>` and `Repository<T>` with `GetById`, `List`, `Add`, `Update`, `Remove`, `Query` (returning `IQueryable`). Register as scoped.

**Acceptance criteria:**
- [ ] `IRepository<T>` interface in `Interfaces/`
- [ ] `Repository<T>` in `Repositories/` uses `ApplicationDbContext`
- [ ] `Query` method returns `IQueryable<T>` so callers can compose
- [ ] DI registration: `AddScoped(typeof(IRepository<>), typeof(Repository<>))`

**Verification:**
- [ ] Build: `dotnet build` returns 0
- [ ] Unit smoke: a throwaway `GET /__smoke/repo` endpoint returns 5 categories (remove after verifying)

**Dependencies:** 3a
**Files likely touched:** `Interfaces/IRepository.cs`, `Repositories/Repository.cs`, `Program.cs`
**Estimated scope:** M

---

##### Task 4b: API response wrapper + exception middleware
**Description:** Standardize all controller responses as `{ success, data, error, meta }`. Add a global exception middleware that maps `ValidationException`, `NotFoundException`, `UnauthorizedAccessException` to proper status codes.

**Acceptance criteria:**
- [ ] `ApiResponse<T>` record with `Success`, `Data`, `Error`, `Meta`
- [ ] `ApiError` record with `Code`, `Message`, `Details?`
- [ ] `ExceptionMiddleware` catches and translates exceptions
- [ ] Custom exceptions in `Exceptions/`: `NotFoundException`, `ValidationException`, `BusinessRuleException`

**Verification:**
- [ ] Throwing `new NotFoundException("X")` from a test endpoint returns 404 with the right shape
- [ ] Unhandled exception returns 500 with generic body (no stack trace leaked)

**Dependencies:** 1d
**Files likely touched:** `Dtos/ApiResponse.cs`, `Dtos/ApiError.cs`, `Middleware/ExceptionMiddleware.cs`, `Exceptions/*.cs`, `Program.cs`
**Estimated scope:** M

---

##### Task 4c: IImageStorage interface + LocalImageStorage
**Description:** Define the strategy interface for image storage and implement local disk storage under `wwwroot/images/`.

**Acceptance criteria:**
- [ ] `IImageStorage` interface: `Task<string> SaveAsync(Stream, string fileName, CancellationToken)`, `Task DeleteAsync(string url, CancellationToken)`, `string GetPublicUrl(string relativePath)`
- [ ] `LocalImageStorage` saves under `wwwroot/images/{yyyy}/{mm}/{guid}.{ext}` and returns `/images/...` URL
- [ ] Validates file is an image (ImageSharp detect) and ≤ 5 MB
- [ ] DI: register `IImageStorage` → `LocalImageStorage`

**Verification:**
- [ ] Test endpoint `POST /__smoke/image` accepts a PNG and returns a URL
- [ ] Saved image is reachable via `GET /images/...`

**Dependencies:** 1b
**Files likely touched:** `Interfaces/IImageStorage.cs`, `Services/LocalImageStorage.cs`, `Program.cs`
**Estimated scope:** M

---

##### Task 4d: IPaymentService interface + MockPaymentService
**Description:** Define payment strategy and ship a mock implementation that always succeeds after a simulated 200 ms delay and returns a fake transaction ID.

**Acceptance criteria:**
- [ ] `IPaymentService` interface: `Task<PaymentResult> ChargeAsync(PaymentRequest, CancellationToken)`
- [ ] `PaymentRequest` includes `Amount`, `Currency`, `OrderId`, `Metadata`
- [ ] `PaymentResult { Success, TransactionId, Message, Status }`
- [ ] `MockPaymentService` returns `Success = true` with `TransactionId = "mock-{guid}"` 100% of the time
- [ ] DI: register `IPaymentService` → `MockPaymentService`

**Verification:**
- [ ] Unit test: `MockPaymentService` returns `Success = true` for any input
- [ ] Test endpoint `POST /__smoke/pay` returns a transaction ID

**Dependencies:** 1d
**Files likely touched:** `Interfaces/IPaymentService.cs`, `Services/MockPaymentService.cs`, `Dtos/PaymentRequest.cs`, `Dtos/PaymentResult.cs`, `Program.cs`
**Estimated scope:** S

---

##### Task 4e: Service registration + DI setup
**Description:** Centralize all DI registrations in a single `ServiceCollectionExtensions.AddApplicationServices` for readability and testability.

**Acceptance criteria:**
- [ ] `Extensions/ServiceCollectionExtensions.cs` with `AddApplicationServices(this IServiceCollection)`
- [ ] Registers: `IImageStorage`, `IPaymentService`, `IRepository<>`, AutoMapper (added in 4f or later), all app services added in later phases
- [ ] `Program.cs` calls `builder.Services.AddApplicationServices(builder.Configuration)`

**Verification:**
- [ ] `dotnet build` returns 0
- [ ] App starts with all previous services still wired

**Dependencies:** 4a, 4c, 4d
**Files likely touched:** `Extensions/ServiceCollectionExtensions.cs`, `Program.cs`
**Estimated scope:** S

---

#### Checkpoint: Foundation
- [ ] `docker-compose up` brings up API + PostgreSQL
- [ ] `curl http://localhost:5000/health` returns 200
- [ ] `curl http://localhost:5000/swagger` lists endpoints
- [ ] Frontend dev server starts with Tailwind + shadcn button working
- [ ] All migrations applied, seed data present in DB
- [ ] `/api/health` reachable via Vite proxy

---

### Phase 2: Authentication & Identity

#### Task 5: Identity & Auth Backend

##### Task 5a: Configure Identity + JWT in Program.cs
**Description:** Wire up ASP.NET Core Identity, configure JWT validation parameters, and add the authentication/authorization middleware.

**Acceptance criteria:**
- [ ] `AddIdentity<ApplicationUser, IdentityRole>` with `IdentityOptions` (password rules, lockout)
- [ ] `AddAuthentication().AddJwtBearer(...)` with symmetric key from config
- [ ] Token validation: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`
- [ ] `app.UseAuthentication()` before `app.UseAuthorization()`
- [ ] JWT settings bound from `Jwt:*` config

**Verification:**
- [ ] `dotnet build` returns 0
- [ ] `GET /products` (added in 7a) without token returns 401
- [ ] With a valid token (issued in 5c) returns 200

**Dependencies:** 4b
**Files likely touched:** `Program.cs`, `appsettings.json`, `appsettings.Development.json`
**Estimated scope:** M

---

##### Task 5b: Implement /auth/register endpoint
**Description:** Create a `POST /auth/register` endpoint that creates a user, assigns the `Customer` role, and returns a JWT.

**Acceptance criteria:**
- [ ] DTO: `RegisterRequest { Email, Password, FullName }`
- [ ] Endpoint: `POST /auth/register` returns `AuthResponse { Token, ExpiresAt, User { Id, Email, FullName, Role } }`
- [ ] Password validated against Identity rules; 400 with field errors on failure
- [ ] Duplicate email returns 409 with code `EMAIL_TAKEN`
- [ ] `curl` test succeeds and creates a row in `AspNetUsers`

**Verification:**
- [ ] `curl -X POST /auth/register -d '{...}'` returns 200 with JWT
- [ ] Decoding the JWT shows `role: "Customer"` and correct `sub`

**Dependencies:** 5a
**Files likely touched:** `Controllers/AuthController.cs`, `Services/AuthService.cs`, `Interfaces/IAuthService.cs`, `Dtos/Auth/RegisterRequest.cs`, `Dtos/Auth/AuthResponse.cs`
**Estimated scope:** M

---

##### Task 5c: Implement /auth/login endpoint
**Description:** Create `POST /auth/login` that validates credentials and returns a JWT with role claims.

**Acceptance criteria:**
- [ ] DTO: `LoginRequest { Email, Password }`
- [ ] Endpoint: `POST /auth/login` returns `AuthResponse`
- [ ] Invalid credentials return 401 with code `INVALID_CREDENTIALS`
- [ ] Token includes `sub` (user id), `email`, `role`, `fullName` claims
- [ ] Token expiry = `Jwt:ExpiresInMinutes` (default 60)

**Verification:**
- [ ] Seeded admin login returns token with `role: "Admin"`
- [ ] Seeded customer login returns token with `role: "Customer"`
- [ ] Decoded JWT contains expected claims

**Dependencies:** 5b
**Files likely touched:** `Controllers/AuthController.cs`, `Services/AuthService.cs`, `Dtos/Auth/LoginRequest.cs`
**Estimated scope:** S

---

##### Task 5d: Add /auth/me endpoint + role attributes
**Description:** Add `GET /auth/me` to return the current user (proves the JWT works) and apply `[Authorize(Roles = "Admin")]` to a placeholder admin controller.

**Acceptance criteria:**
- [ ] `GET /auth/me` returns `UserDto { Id, Email, FullName, Role }` from claims
- [ ] `[Authorize]` attribute on a test controller
- [ ] `[Authorize(Roles = "Admin")]` on `AdminTestController` test endpoint
- [ ] Customer token → 403 on admin endpoint; admin token → 200

**Verification:**
- [ ] `curl -H "Authorization: Bearer <token>" /auth/me` returns the user
- [ ] Customer token on `/AdminTest/ping` returns 403

**Dependencies:** 5c
**Files likely touched:** `Controllers/AuthController.cs`, `Controllers/AdminTestController.cs`
**Estimated scope:** S

---

#### Task 6: Auth Frontend

##### Task 6a: Zustand auth store
**Description:** Create a Zustand store with `token`, `user`, `isAuthenticated`, `isAdmin` selectors, and `login`, `logout`, `setUser` actions. Persist `token` to `localStorage`.

**Acceptance criteria:**
- [ ] `useAuthStore` with persisted `token` via `zustand/middleware/persist`
- [ ] `isAdmin` computed selector returns `user?.role === "Admin"`
- [ ] `logout()` clears token and user
- [ ] `hydrated` flag exposed for guarding initial render

**Verification:**
- [ ] Unit-style manual test: `useAuthStore.getState().login(...)` populates state
- [ ] Reload preserves `token` from localStorage

**Dependencies:** 2c
**Files likely touched:** `frontend/src/stores/authStore.ts`
**Estimated scope:** S

---

##### Task 6b: Axios instance with auth interceptor
**Description:** Create a shared axios instance that attaches the JWT, handles 401 (clears auth + redirects to login), and exposes typed error parsing.

**Acceptance criteria:**
- [ ] `lib/api.ts` exports a configured `axios` instance with `baseURL` from `VITE_API_URL`
- [ ] Request interceptor reads token from `useAuthStore` and adds `Authorization` header
- [ ] Response interceptor on 401 → `useAuthStore.getState().logout()` + redirect to `/login`
- [ ] Exports `ApiError` helper that extracts `code` and `message` from response body

**Verification:**
- [ ] Expired/invalid token triggers logout + redirect
- [ ] Valid token sent as `Authorization: Bearer <token>`

**Dependencies:** 6a
**Files likely touched:** `frontend/src/lib/api.ts`, `frontend/src/lib/errors.ts`
**Estimated scope:** S

---

##### Task 6c: Login + Register pages with shadcn forms
**Description:** Build `/login` and `/register` pages using shadcn `Form` + `Input` + `Button` + react-hook-form + zod validation.

**Acceptance criteria:**
- [ ] `/login` form: email, password, submit button. On success, store token + redirect to `/`
- [ ] `/register` form: full name, email, password, confirm password. On success, auto-login
- [ ] Server errors displayed inline (e.g., `INVALID_CREDENTIALS` → "Invalid email or password")
- [ ] Loading state disables submit
- [ ] Form fields use shadcn `Form` components

**Verification:**
- [ ] Registering a new user via UI creates the user and logs in
- [ ] Wrong password shows error and stays on `/login`

**Dependencies:** 5c, 6b
**Files likely touched:** `frontend/src/pages/Login.tsx`, `frontend/src/pages/Register.tsx`, `frontend/src/lib/schemas/auth.ts`
**Estimated scope:** M

---

##### Task 6d: Protected + role-based route guards
**Description:** Add `<RequireAuth />` and `<RequireRole role="Admin" />` wrapper components used in route definitions.

**Acceptance criteria:**
- [ ] `<RequireAuth />` redirects to `/login?next=<path>` when not authenticated
- [ ] `<RequireRole role="Admin" />` shows 403 page when authenticated but wrong role
- [ ] Routes in `App.tsx` use these guards
- [ ] `next` query param is honored after successful login

**Verification:**
- [ ] Logged-out user visiting `/checkout` is redirected to `/login?next=/checkout`
- [ ] Customer visiting `/admin` sees 403 page
- [ ] Admin visiting `/admin` sees admin layout

**Dependencies:** 6a
**Files likely touched:** `frontend/src/components/RequireAuth.tsx`, `frontend/src/components/RequireRole.tsx`, `frontend/src/App.tsx`, `frontend/src/pages/Forbidden.tsx`
**Estimated scope:** M

---

##### Task 6e: Navbar with auth state
**Description:** Update `<Navbar />` to show Login/Register when logged out, and user menu (with logout) + cart icon placeholder when logged in.

**Acceptance criteria:**
- [ ] Logged-out navbar: Login, Register buttons
- [ ] Logged-in navbar: cart icon (badge placeholder), user dropdown with "My Orders" + "Logout"
- [ ] Admin users see an extra "Admin" link
- [ ] Logout clears store and routes to `/`

**Verification:**
- [ ] Navbar state changes immediately on login/logout (no reload)
- [ ] Clicking Logout returns to `/` and navbar shows Login/Register

**Dependencies:** 6c, 6d
**Files likely touched:** `frontend/src/components/Navbar.tsx`, `frontend/src/components/UserMenu.tsx`
**Estimated scope:** M

---

#### Checkpoint: Auth
- [ ] User can register and login via UI
- [ ] Backend issues JWT with role claim
- [ ] Customer cannot access `/admin` (403)
- [ ] Admin can access `/admin` (200)
- [ ] Token persists across reload
- [ ] 401 from API triggers logout + redirect

---

### Phase 3: Product Catalog (Customer)

#### Task 7: Product API Endpoints

##### Task 7a: GET /products with pagination, filtering, sorting
**Description:** Public endpoint returning a paginated list of active products with optional category, search, sort, and price-range filters.

**Acceptance criteria:**
- [ ] Query params: `page` (default 1), `pageSize` (default 20, max 100), `category` (slug), `q` (search), `sort` (`price_asc|price_desc|name_asc|name_desc|newest`), `minPrice`, `maxPrice`
- [ ] Response: `{ items: ProductListItem[], meta: { page, pageSize, total, totalPages } }`
- [ ] Search matches `Name` and `Description` (case-insensitive `ILIKE`)
- [ ] Filtered by `IsActive = true` by default
- [ ] Cache headers: `Cache-Control: public, max-age=60`

**Verification:**
- [ ] `GET /products?page=1&pageSize=10` returns 10 items + meta
- [ ] `GET /products?q=shirt&sort=price_asc` returns matching items sorted
- [ ] `GET /products?category=electronics&minPrice=10&maxPrice=100` filters correctly

**Dependencies:** 3f, 4a
**Files likely touched:** `Controllers/ProductsController.cs`, `Services/ProductService.cs`, `Interfaces/IProductService.cs`, `Dtos/Product/ProductListItem.cs`, `Dtos/Product/PagedResponse.cs`
**Estimated scope:** M

---

##### Task 7b: GET /products/:id
**Description:** Public endpoint returning a single product with its images and category.

**Acceptance criteria:**
- [ ] Response: `ProductDetail { Id, Name, Slug, Description, Price, Stock, Category, Images[] }`
- [ ] 404 with code `PRODUCT_NOT_FOUND` if missing or `IsActive = false`
- [ ] Images ordered by `SortOrder` ascending

**Verification:**
- [ ] `GET /products/1` returns product with images
- [ ] `GET /products/99999` returns 404

**Dependencies:** 7a
**Files likely touched:** `Controllers/ProductsController.cs`, `Dtos/Product/ProductDetail.cs`
**Estimated scope:** S

---

##### Task 7c: GET /categories
**Description:** Public endpoint listing all categories as a flat list with product counts.

**Acceptance criteria:**
- [ ] Response: `CategoryDto[] { Id, Name, Slug, ProductCount }`
- [ ] Ordered by `Name` ascending
- [ ] `ProductCount` counts only active products

**Verification:**
- [ ] `GET /categories` returns all seeded categories with correct counts
- [ ] Deactivating a product decrements its category's count

**Dependencies:** 3f
**Files likely touched:** `Controllers/CategoriesController.cs`, `Dtos/Category/CategoryDto.cs`
**Estimated scope:** S

---

#### Task 8: Product Catalog UI

##### Task 8a: Product card component + grid
**Description:** Reusable `<ProductCard />` showing image, name, price, and an "Add to cart" button (placeholder action for now).

**Acceptance criteria:**
- [ ] `<ProductCard product={...} />` renders image (with fallback), name, formatted price
- [ ] Stock = 0 → "Out of stock" badge, disabled button
- [ ] Card is keyboard-focusable and links to `/products/:slug`
- [ ] Responsive grid: 1 col mobile, 2 tablet, 3–4 desktop (Tailwind)

**Verification:**
- [ ] Card renders correctly for a seeded product
- [ ] Out-of-stock product shows disabled state

**Dependencies:** 7a, 2b
**Files likely touched:** `frontend/src/components/ProductCard.tsx`, `frontend/src/lib/format.ts`
**Estimated scope:** S

---

##### Task 8b: Category filter + search bar
**Description:** Sidebar (desktop) / collapsible (mobile) with category checkboxes and a search input. URL is the source of truth: filters reflect in `?q=...&category=...`.

**Acceptance criteria:**
- [ ] Categories fetched via TanStack Query and rendered as checkboxes
- [ ] Search input debounced 300 ms before updating URL
- [ ] Selecting a category updates URL and re-fetches products
- [ ] "Clear filters" button resets URL

**Verification:**
- [ ] Typing in search updates products after debounce
- [ ] Selecting a category narrows results and updates URL

**Dependencies:** 7a, 7c
**Files likely touched:** `frontend/src/components/CategoryFilter.tsx`, `frontend/src/components/SearchBar.tsx`, `frontend/src/hooks/useDebounce.ts`
**Estimated scope:** M

---

##### Task 8c: Pagination component
**Description:** Reusable `<Pagination />` showing current page, total pages, prev/next buttons.

**Acceptance criteria:**
- [ ] Renders prev/next + first/last + numbered buttons (with ellipsis for > 7 pages)
- [ ] URL is the source of truth (`?page=`)
- [ ] Disabled state at first/last page

**Verification:**
- [ ] Clicking page 3 updates URL and re-fetches
- [ ] Browser back/forward preserves pagination state

**Dependencies:** 7a
**Files likely touched:** `frontend/src/components/Pagination.tsx`
**Estimated scope:** S

---

##### Task 8d: Product list page
**Description:** `/products` page composing `SearchBar`, `CategoryFilter`, `Pagination`, and a `ProductCard` grid.

**Acceptance criteria:**
- [ ] Reads filters from URL on mount
- [ ] Skeleton loaders during fetch
- [ ] Empty state when no products match
- [ ] Layout: filter sidebar (desktop) / top drawer (mobile) + product grid

**Verification:**
- [ ] Page loads with seed data
- [ ] Combined filter + search + pagination works end-to-end

**Dependencies:** 8a, 8b, 8c
**Files likely touched:** `frontend/src/pages/ProductList.tsx`
**Estimated scope:** M

---

##### Task 8e: Product detail page
**Description:** `/products/:slug` page showing gallery, name, price, description, stock, and "Add to cart" button (wired in 10d).

**Acceptance criteria:**
- [ ] Fetches product by slug (need to add slug support to API or use id)
- [ ] Image gallery with main image + thumbnails
- [ ] Quantity selector (1–stock)
- [ ] Add to cart button is visible but not yet wired (label: "Add to cart")

**Verification:**
- [ ] `/products/<seeded-slug>` renders detail page
- [ ] Out-of-stock shows disabled button

**Dependencies:** 7b
**Files likely touched:** `frontend/src/pages/ProductDetail.tsx`, `frontend/src/components/ImageGallery.tsx`
**Estimated scope:** M

---

##### Task 8f: TanStack Query hooks for products
**Description:** Centralize product queries in `hooks/products.ts` for reuse and consistent cache keys.

**Acceptance criteria:**
- [ ] `useProducts(filters)` → list with filters
- [ ] `useProduct(slug)` → single product
- [ ] `useCategories()` → categories list
- [ ] Cache keys: `['products', filters]`, `['product', slug]`, `['categories']`
- [ ] `staleTime: 60_000` for products/categories

**Verification:**
- [ ] Multiple components using `useCategories()` share the cache
- [ ] Refetching happens after 60 s of staleness

**Dependencies:** 7a, 7b, 7c
**Files likely touched:** `frontend/src/hooks/products.ts`
**Estimated scope:** S

---

#### Checkpoint: Catalog
- [ ] `/products` shows all seeded products with images
- [ ] Search and category filter work
- [ ] Pagination updates URL and fetches correctly
- [ ] `/products/:slug` shows product detail
- [ ] Skeleton loaders and empty states render

---

### Phase 4: Cart & Checkout

#### Task 9: Cart API Endpoints

##### Task 9a: GET /cart
**Description:** Returns the current authenticated user's cart with items, totals, and product snapshot.

**Acceptance criteria:**
- [ ] Creates a cart on first call (idempotent)
- [ ] Response: `CartDto { Id, Items: CartItemDto[], Subtotal, ItemCount }`
- [ ] `CartItemDto` includes `ProductId, ProductName, ProductImage, UnitPrice, Quantity, LineTotal`
- [ ] Snapshot `UnitPrice` from product at fetch time (so price changes don't affect open cart display — but `POST /orders` re-validates, see 11a)

**Verification:**
- [ ] First `GET /cart` returns empty cart
- [ ] Adding items (9b) and fetching returns them with correct totals

**Dependencies:** 3c, 5d
**Files likely touched:** `Controllers/CartController.cs`, `Services/CartService.cs`, `Interfaces/ICartService.cs`, `Dtos/Cart/CartDto.cs`, `Dtos/Cart/CartItemDto.cs`
**Estimated scope:** M

---

##### Task 9b: POST /cart/items, PUT /cart/items/:id, DELETE /cart/items/:id
**Description:** CRUD for individual cart items.

**Acceptance criteria:**
- [ ] `POST /cart/items` body `{ productId, quantity }`: adds or updates line; rejects `quantity > stock` with 400 `INSUFFICIENT_STOCK`
- [ ] `PUT /cart/items/:id` body `{ quantity }`: updates; `quantity = 0` removes; rejects with 400 if exceeds stock
- [ ] `DELETE /cart/items/:id`: removes line
- [ ] All endpoints return the updated `CartDto` (9a shape)
- [ ] Authenticated user only; 401 if anonymous

**Verification:**
- [ ] Adding 2 of the same product results in 1 line with quantity 2
- [ ] Setting quantity to 0 removes the line
- [ ] Adding more than stock returns 400

**Dependencies:** 9a
**Files likely touched:** `Controllers/CartController.cs`, `Services/CartService.cs`, `Dtos/Cart/AddCartItemRequest.cs`, `Dtos/Cart/UpdateCartItemRequest.cs`
**Estimated scope:** M

---

##### Task 9c: DELETE /cart (clear)
**Description:** Clears all items from the current user's cart.

**Acceptance criteria:**
- [ ] `DELETE /cart` returns 204
- [ ] Idempotent (clearing an empty cart also returns 204)
- [ ] Authenticated user only

**Verification:**
- [ ] Cart with items → DELETE → next GET returns empty cart
- [ ] Second DELETE still returns 204

**Dependencies:** 9a
**Files likely touched:** `Controllers/CartController.cs`
**Estimated scope:** S

---

#### Task 10: Cart UI

##### Task 10a: Cart store (TanStack Query) + hook
**Description:** Use TanStack Query for cart server state (since it's source-of-truth in DB). Provide `useCart()` returning `{ cart, add, update, remove, clear }`.

**Acceptance criteria:**
- [ ] `useCart()` reads `['cart']` query key
- [ ] Mutations invalidate `['cart']` on success
- [ ] Optimistic update for `update` and `remove` (rollback on error)
- [ ] Toast on error (e.g., `INSUFFICIENT_STOCK`)

**Verification:**
- [ ] Adding an item updates UI without full page reload
- [ ] Failed add rolls back optimistic update

**Dependencies:** 9a, 6b
**Files likely touched:** `frontend/src/hooks/cart.ts`, `frontend/src/hooks/useToast.ts`
**Estimated scope:** M

---

##### Task 10b: Cart icon with item count badge
**Description:** Cart icon in `<Navbar />` shows a badge with `itemCount`. Clicking opens the cart sheet (10c).

**Acceptance criteria:**
- [ ] Badge shows `itemCount` from `useCart()`; hidden when 0
- [ ] Icon wrapped in shadcn `<Button variant="ghost">` with focus state
- [ ] Mobile: 44px touch target

**Verification:**
- [ ] Badge appears after adding an item
- [ ] Disappears after clearing cart

**Dependencies:** 6e, 10a
**Files likely touched:** `frontend/src/components/CartIcon.tsx`, `frontend/src/components/Navbar.tsx`
**Estimated scope:** S

---

##### Task 10c: Cart sheet (shadcn Sheet)
**Description:** Slide-in sheet listing cart items with thumbnail, name, price, quantity stepper, remove, subtotal, and "Checkout" button.

**Acceptance criteria:**
- [ ] Uses shadcn `<Sheet>` opened from cart icon
- [ ] Quantity stepper calls `update` mutation
- [ ] Remove button calls `remove` mutation with confirm
- [ ] Subtotal updates live
- [ ] "Checkout" button is disabled when cart is empty

**Verification:**
- [ ] Opening the sheet shows current items
- [ ] Changing quantity updates subtotal and badge

**Dependencies:** 10a, 10b
**Files likely touched:** `frontend/src/components/CartSheet.tsx`, `frontend/src/components/CartItemRow.tsx`
**Estimated scope:** M

---

##### Task 10d: Add-to-cart from product pages
**Description:** Wire the "Add to cart" buttons on `<ProductCard />` and `<ProductDetail />` to the cart mutation.

**Acceptance criteria:**
- [ ] Card button: adds 1, shows toast "Added to cart"
- [ ] Detail page: respects quantity selector
- [ ] Both disabled when out of stock
- [ ] Cart icon badge updates immediately (optimistic)

**Verification:**
- [ ] Clicking "Add to cart" on a card adds item to cart sheet
- [ ] Out-of-stock card button is disabled

**Dependencies:** 10a, 8a, 8e
**Files likely touched:** `frontend/src/components/ProductCard.tsx`, `frontend/src/pages/ProductDetail.tsx`
**Estimated scope:** S

---

#### Task 11: Checkout API

##### Task 11a: POST /orders
**Description:** Creates an order from the current cart. Re-validates stock and prices, charges via `IPaymentService`, deducts stock, clears cart, returns the created order.

**Acceptance criteria:**
- [ ] Request: `CheckoutRequest { ShippingAddress, ShippingFee? }`
- [ ] Validates cart not empty (400 `EMPTY_CART`)
- [ ] Re-validates stock for every item (400 `INSUFFICIENT_STOCK` with offending items)
- [ ] Re-calculates `Subtotal` from current product prices
- [ ] Calls `IPaymentService.ChargeAsync`; on failure, returns 402 `PAYMENT_FAILED` and does not deduct stock
- [ ] On success: creates `Order` + `OrderItem` rows, deducts `Product.Stock`, clears `Cart`
- [ ] Response: `OrderDto` (full order with items)

**Verification:**
- [ ] Empty cart → 400 `EMPTY_CART`
- [ ] Stock < requested → 400 `INSUFFICIENT_STOCK`, cart unchanged
- [ ] Happy path → 201 with order, cart empty, stock decremented in DB

**Dependencies:** 3d, 4d, 9a
**Files likely touched:** `Controllers/OrdersController.cs`, `Services/OrderService.cs`, `Interfaces/IOrderService.cs`, `Dtos/Order/CheckoutRequest.cs`, `Dtos/Order/OrderDto.cs`
**Estimated scope:** L

---

##### Task 11b: GET /orders, GET /orders/:id
**Description:** List the current user's orders and fetch a single order.

**Acceptance criteria:**
- [ ] `GET /orders?page=&pageSize=` returns user's orders, newest first, paginated
- [ ] `GET /orders/:id` returns order with items; 404 if not owned by user
- [ ] OrderDto includes `Items[]` with product snapshot

**Verification:**
- [ ] After 11a, `GET /orders` shows the new order
- [ ] Another user's order ID returns 404

**Dependencies:** 11a
**Files likely touched:** `Controllers/OrdersController.cs`, `Dtos/Order/OrderSummaryDto.cs`
**Estimated scope:** S

---

#### Task 12: Checkout UI

##### Task 12a: Checkout form (shipping)
**Description:** `/checkout` page with shipping address form (full name, street, city, postal code, country, phone). Protected route.

**Acceptance criteria:**
- [ ] Form uses shadcn `Form` + react-hook-form + zod
- [ ] Pre-fills `FullName` from `useAuthStore`
- [ ] Shows order summary sidebar with items, subtotal, shipping, total
- [ ] "Place order" button submits to `POST /orders`; disabled while submitting

**Verification:**
- [ ] Form validation: required fields, valid postal code format
- [ ] Submitting creates an order and routes to confirmation page

**Dependencies:** 11a, 6d
**Files likely touched:** `frontend/src/pages/Checkout.tsx`, `frontend/src/components/OrderSummary.tsx`, `frontend/src/lib/schemas/checkout.ts`
**Estimated scope:** M

---

##### Task 12b: Order confirmation page
**Description:** `/orders/:id` route renders order confirmation after a successful checkout.

**Acceptance criteria:**
- [ ] Shows "Thank you" message, order ID, summary, ETA placeholder
- [ ] "Continue shopping" link → `/products`
- [ ] "View my orders" link → `/orders`

**Verification:**
- [ ] After successful checkout, lands on confirmation page
- [ ] Refreshing the page still shows the order

**Dependencies:** 11b
**Files likely touched:** `frontend/src/pages/OrderConfirmation.tsx`
**Estimated scope:** S

---

##### Task 12c: Order history page
**Description:** `/orders` page lists the user's past orders with status badge and links to detail.

**Acceptance criteria:**
- [ ] Lists orders, newest first
- [ ] Each row: order ID, date, total, status badge, "View" link
- [ ] Empty state if no orders
- [ ] Pagination if more than 20 orders

**Verification:**
- [ ] Customer sees only their own orders
- [ ] Clicking a row navigates to `/orders/:id`

**Dependencies:** 11b
**Files likely touched:** `frontend/src/pages/OrderHistory.tsx`, `frontend/src/components/OrderStatusBadge.tsx`
**Estimated scope:** M

---

#### Checkpoint: Cart & Checkout
- [ ] Customer can add items to cart from list and detail pages
- [ ] Cart sheet shows live updates
- [ ] Checkout creates an order, decrements stock, clears cart
- [ ] Order confirmation + history pages work
- [ ] Payment failure (mock) handled gracefully

---

### Phase 5: Admin Panel

#### Task 13: Admin — Product Management API

##### Task 13a: GET /admin/products, POST /admin/products
**Description:** Admin-only endpoints to list all products (including inactive) and create a new product.

**Acceptance criteria:**
- [ ] `GET /admin/products?page=&pageSize=&q=&isActive=` returns paged products (admin view: includes `IsActive`, `CreatedAt`)
- [ ] `POST /admin/products` body: `CreateProductRequest { Name, Slug, Description, Price, Stock, CategoryId, IsActive }`; returns 201 with created product
- [ ] Slug uniqueness enforced (409 `SLUG_TAKEN`)
- [ ] All endpoints require `Role = Admin`

**Verification:**
- [ ] Customer token → 403
- [ ] Admin can create a product and see it in the list

**Dependencies:** 7a, 3b
**Files likely touched:** `Controllers/Admin/AdminProductsController.cs`, `Services/AdminProductService.cs`, `Dtos/Admin/CreateProductRequest.cs`, `Dtos/Admin/AdminProductListItem.cs`
**Estimated scope:** M

---

##### Task 13b: PUT /admin/products/:id, DELETE /admin/products/:id
**Description:** Update and delete products as admin.

**Acceptance criteria:**
- [ ] `PUT /admin/products/:id` updates any field; returns 404 if not found
- [ ] `DELETE /admin/products/:id` soft-deletes (sets `IsActive = false`) by default; hard delete via `?hard=true`
- [ ] Hard delete blocked with 409 `PRODUCT_IN_USE` if referenced by any order item or cart
- [ ] All endpoints require `Role = Admin`

**Verification:**
- [ ] Update changes persist and reflect on `/products`
- [ ] Soft-deleted product disappears from public catalog but remains in admin list
- [ ] Hard delete of a product in an order returns 409

**Dependencies:** 13a
**Files likely touched:** `Controllers/Admin/AdminProductsController.cs`, `Dtos/Admin/UpdateProductRequest.cs`
**Estimated scope:** M

---

##### Task 13c: POST /admin/products/:id/images, DELETE /admin/products/:id/images/:imageId
**Description:** Image upload and management using `IImageStorage`.

**Acceptance criteria:**
- [ ] `POST /admin/products/:id/images` accepts multipart/form-data with one or more files
- [ ] Saves via `IImageStorage`, persists `ProductImage` rows
- [ ] `DELETE /admin/products/:id/images/:imageId` removes the image and the file
- [ ] Rejects non-image files and > 5 MB
- [ ] All endpoints require `Role = Admin`

**Verification:**
- [ ] Uploading a JPG creates a `ProductImage` row and file under `wwwroot/images/...`
- [ ] Image is served via `/images/...`
- [ ] Deleting removes both the row and the file

**Dependencies:** 13a, 4c
**Files likely touched:** `Controllers/Admin/AdminProductImagesController.cs`, `Dtos/Admin/UploadImageResponse.cs`
**Estimated scope:** M

---

#### Task 14: Admin — Product Management UI

##### Task 14a: Product data table
**Description:** `/admin/products` page with shadcn `<DataTable />` showing all products with search, status filter, pagination, and row actions.

**Acceptance criteria:**
- [ ] Columns: image, name, category, price, stock, status (active/inactive), actions
- [ ] Search by name, status filter dropdown
- [ ] Row actions: Edit, Add image, Delete (with confirm)
- [ ] Pagination controls

**Verification:**
- [ ] Admin sees all products; search narrows results
- [ ] Toggling status from the row updates the list

**Dependencies:** 13a
**Files likely touched:** `frontend/src/pages/admin/AdminProducts.tsx`, `frontend/src/components/admin/ProductTable.tsx`
**Estimated scope:** M

---

##### Task 14b: Add/Edit product form
**Description:** Modal or dedicated page with form for creating/editing a product, including image upload (multi-file).

**Acceptance criteria:**
- [ ] Form fields: name, slug (auto-generated from name, editable), description, price, stock, category, isActive
- [ ] Image upload field accepts multiple files; thumbnails show after upload
- [ ] Validates required fields and price > 0
- [ ] On submit, closes modal and refreshes table

**Verification:**
- [ ] Creating a product via UI makes it appear in the table and public catalog
- [ ] Editing a product updates it
- [ ] Image upload shows in product detail

**Dependencies:** 14a, 13b, 13c
**Files likely touched:** `frontend/src/pages/admin/AdminProductForm.tsx`, `frontend/src/components/admin/ImageUploader.tsx`
**Estimated scope:** M

---

##### Task 14c: Delete confirmation dialog
**Description:** Confirm-before-delete using shadcn `<AlertDialog />`. Show product name in the message.

**Acceptance criteria:**
- [ ] Clicking Delete opens `<AlertDialog />` with product name and "This cannot be undone" copy
- [ ] Confirm calls delete mutation; cancel closes dialog
- [ ] Toast on success or error

**Verification:**
- [ ] Deleting soft-removes product from public catalog
- [ ] Cancelling leaves the product in the table

**Dependencies:** 14a, 13b
**Files likely touched:** `frontend/src/components/admin/DeleteProductDialog.tsx`
**Estimated scope:** S

---

#### Task 15: Admin — Order Management API

##### Task 15a: GET /admin/orders
**Description:** Admin endpoint to list all orders with filters.

**Acceptance criteria:**
- [ ] Query: `page`, `pageSize`, `status`, `q` (search by user email or order id), `from`, `to` (date range)
- [ ] Response: paginated `AdminOrderListItem[]` with user email, status, total, created at
- [ ] Requires `Role = Admin`

**Verification:**
- [ ] Filter by `status=Paid` returns only paid orders
- [ ] Date range filter narrows results

**Dependencies:** 11a
**Files likely touched:** `Controllers/Admin/AdminOrdersController.cs`, `Dtos/Admin/AdminOrderListItem.cs`
**Estimated scope:** M

---

##### Task 15b: GET /admin/orders/:id
**Description:** Admin order detail including user info, items, shipping address, and payment info.

**Acceptance criteria:**
- [ ] Response: full `AdminOrderDetail` with `User { Email, FullName }`, items, shipping, payment
- [ ] 404 if not found
- [ ] Requires `Role = Admin`

**Verification:**
- [ ] Admin can view any order's full detail
- [ ] Customer info is included

**Dependencies:** 15a
**Files likely touched:** `Controllers/Admin/AdminOrdersController.cs`, `Dtos/Admin/AdminOrderDetail.cs`
**Estimated scope:** S

---

##### Task 15c: PUT /admin/orders/:id/status
**Description:** Admin status transitions with guard rails.

**Acceptance criteria:**
- [ ] Body: `{ status }`
- [ ] Allowed transitions: `Pending → Paid | Cancelled`, `Paid → Shipped | Cancelled`, `Shipped → Delivered`, `Delivered` and `Cancelled` are terminal
- [ ] Invalid transition returns 409 `INVALID_TRANSITION`
- [ ] On `Cancelled`: restock items
- [ ] Requires `Role = Admin`

**Verification:**
- [ ] Valid transition updates status and persists
- [ ] Invalid transition returns 409
- [ ] Cancelling a `Paid` order restocks items

**Dependencies:** 15b
**Files likely touched:** `Controllers/Admin/AdminOrdersController.cs`, `Services/AdminOrderService.cs`
**Estimated scope:** M

---

#### Task 16: Admin — Order Management UI

##### Task 16a: Orders table with status filter
**Description:** `/admin/orders` page with shadcn DataTable, status filter dropdown, date range picker, search.

**Acceptance criteria:**
- [ ] Columns: order id, user email, total, status, created at, actions
- [ ] Status filter chips/dropdown
- [ ] Date range filter
- [ ] Row click opens detail

**Verification:**
- [ ] Filtering by status updates results
- [ ] Clicking a row opens detail

**Dependencies:** 15a
**Files likely touched:** `frontend/src/pages/admin/AdminOrders.tsx`
**Estimated scope:** M

---

##### Task 16b: Order detail view
**Description:** `/admin/orders/:id` page showing user, items, shipping, payment, and status update controls.

**Acceptance criteria:**
- [ ] Shows user info, item list with thumbnails, shipping address, payment txn id
- [ ] Status update dropdown with only valid transitions (computed client-side)
- [ ] Print-friendly layout (basic `@media print` styles)

**Verification:**
- [ ] Status dropdown only shows valid transitions
- [ ] Updating status reflects in the table

**Dependencies:** 15b, 15c
**Files likely touched:** `frontend/src/pages/admin/AdminOrderDetail.tsx`
**Estimated scope:** M

---

##### Task 16c: Status update dropdown
**Description:** Reusable `<OrderStatusSelect />` that fetches valid transitions and submits updates.

**Acceptance criteria:**
- [ ] Component takes `order` and `onUpdated` callback
- [ ] Calls `PUT /admin/orders/:id/status` and invalidates `['admin-orders']`
- [ ] Shows loading + error states

**Verification:**
- [ ] Selecting a new status persists and updates UI
- [ ] Invalid transitions are not offered as options

**Dependencies:** 15c, 16b
**Files likely touched:** `frontend/src/components/admin/OrderStatusSelect.tsx`
**Estimated scope:** S

---

#### Task 17: Admin — Dashboard

##### Task 17a: Dashboard stats endpoints
**Description:** Backend endpoints powering the admin dashboard cards and charts.

**Acceptance criteria:**
- [ ] `GET /admin/dashboard/summary` returns `{ totalOrders, totalRevenue, totalCustomers, totalProducts, lowStockCount }`
- [ ] `GET /admin/dashboard/sales?days=30` returns daily sales series `[ { date, total, orderCount } ]`
- [ ] `GET /admin/dashboard/recent-orders?limit=10` returns latest orders
- [ ] `GET /admin/dashboard/low-stock?threshold=10` returns products with `Stock <= threshold`
- [ ] All require `Role = Admin`

**Verification:**
- [ ] Numbers match direct DB queries
- [ ] Sales series has the requested day count

**Dependencies:** 11a, 3b
**Files likely touched:** `Controllers/Admin/AdminDashboardController.cs`, `Services/DashboardService.cs`, `Dtos/Admin/DashboardSummary.cs`, `Dtos/Admin/SalesPoint.cs`
**Estimated scope:** L

---

##### Task 17b: Dashboard cards UI
**Description:** `/admin` page (or `/admin/dashboard`) with 4 KPI cards: orders, revenue, customers, low stock count.

**Acceptance criteria:**
- [ ] 4 cards in a responsive grid
- [ ] Each card: title, value, optional delta vs previous period
- [ ] Loading skeleton state

**Verification:**
- [ ] Cards display correct values from API
- [ ] Numbers update on refresh

**Dependencies:** 17a
**Files likely touched:** `frontend/src/pages/admin/AdminDashboard.tsx`, `frontend/src/components/admin/StatCard.tsx`
**Estimated scope:** S

---

##### Task 17c: Charts (sales line + recent orders list + low stock table)
**Description:** Add a sales line chart for the last 30 days, a recent-orders list, and a low-stock products table.

**Acceptance criteria:**
- [ ] Sales line chart using Recharts (already in the Vite bundle, lightweight)
- [ ] Recent orders: 10 rows with status badges linking to detail
- [ ] Low stock table: product name, stock, "Edit" link
- [ ] All sections have loading + empty states

**Verification:**
- [ ] Chart renders with data
- [ ] Recent orders link to detail page
- [ ] Low stock list matches API

**Dependencies:** 17a, 17b
**Files likely touched:** `frontend/src/components/admin/SalesChart.tsx`, `frontend/src/components/admin/RecentOrdersList.tsx`, `frontend/src/components/admin/LowStockTable.tsx`
**Estimated scope:** M

---

#### Checkpoint: Admin
- [ ] Admin can CRUD products, upload images
- [ ] Admin can list orders, view detail, update status
- [ ] Cancelling an order restocks items
- [ ] Dashboard shows summary, sales chart, recent orders, low stock

---

### Phase 6: Testing & Polish

#### Task 18: Backend Testing

##### Task 18a: Test infrastructure
**Description:** Set up xUnit + Moq + FluentAssertions + a test base class that uses Testcontainers for PostgreSQL.

**Acceptance criteria:**
- [ ] `tests/MiniEcommerce.Api.Tests/` xUnit project
- [ ] `Testcontainers.PostgreSql` for ephemeral DB per test class
- [ ] `WebApplicationFactory<Program>` for integration tests with a test JWT signing key
- [ ] Test base class applies migrations and seeds a known dataset
- [ ] `dotnet test` runs all tests

**Verification:**
- [ ] `dotnet test` returns 0 with at least the smoke test
- [ ] Tests run in CI without external services (uses Testcontainers)

**Dependencies:** 4b
**Files likely touched:** `tests/MiniEcommerce.Api.Tests/MiniEcommerce.Api.Tests.csproj`, `tests/TestBase.cs`, `tests/IntegrationTestBase.cs`
**Estimated scope:** M

---

##### Task 18b: Unit tests for services
**Description:** Unit tests for `AuthService`, `CartService`, `OrderService`, `ProductService` with mocked repositories.

**Acceptance criteria:**
- [ ] At least 3 tests per service covering happy path + 2 edge cases
- [ ] Uses Moq for `IRepository<T>` and `UserManager<ApplicationUser>`
- [ ] Asserts on service return values and side effects (calls to mocks)

**Verification:**
- [ ] `dotnet test --filter Category=Unit` returns 0
- [ ] Coverage report shows services > 70% covered

**Dependencies:** 18a
**Files likely touched:** `tests/Services/AuthServiceTests.cs`, `tests/Services/CartServiceTests.cs`, `tests/Services/OrderServiceTests.cs`, `tests/Services/ProductServiceTests.cs`
**Estimated scope:** M

---

##### Task 18c: Integration tests for controllers
**Description:** Integration tests for `AuthController`, `ProductsController`, `CartController`, `OrdersController` (happy paths + auth checks).

**Acceptance criteria:**
- [ ] Auth: register, login, `/auth/me` with valid/invalid token
- [ ] Products: list, detail, 404
- [ ] Cart: add/update/remove/clear; rejects anonymous
- [ ] Orders: create from cart, list, detail, ownership enforced
- [ ] All use `WebApplicationFactory` and a real (Testcontainer) DB

**Verification:**
- [ ] `dotnet test --filter Category=Integration` returns 0
- [ ] Tests are independent (no shared state between tests)

**Dependencies:** 18a
**Files likely touched:** `tests/Controllers/AuthControllerTests.cs`, `tests/Controllers/ProductsControllerTests.cs`, `tests/Controllers/CartControllerTests.cs`, `tests/Controllers/OrdersControllerTests.cs`
**Estimated scope:** M

---

#### Task 19: Documentation

##### Task 19a: README + setup instructions
**Description:** Top-level `README.md` with project overview, architecture, prerequisites, and step-by-step setup.

**Acceptance criteria:**
- [ ] Sections: Overview, Tech stack, Architecture diagram (ASCII ok), Prerequisites, Quick start, Test, Deploy
- [ ] Quick start uses `docker-compose up` and shows how to seed/login
- [ ] Screenshots or gifs of the catalog and admin (placeholders ok)

**Verification:**
- [ ] A new dev can follow the README and have the app running
- [ ] All commands in the README are tested and work

**Dependencies:** None
**Files likely touched:** `README.md`
**Estimated scope:** M

---

##### Task 19b: Swagger annotations
**Description:** Enrich Swagger with `[SwaggerResponse]`, `[ProducesResponseType]`, summaries, and example payloads.

**Acceptance criteria:**
- [ ] Every controller action has `[ProducesResponseType]` for all status codes
- [ ] `[SwaggerOperation(Summary = "...")]` on each action
- [ ] DTOs have `[SwaggerSchema]` descriptions
- [ ] Swagger UI shows the full schema and example responses

**Verification:**
- [ ] Visit `/swagger` and see summaries + schemas for every endpoint
- [ ] No warning logs about missing XML comments (if XML comments enabled)

**Dependencies:** None
**Files likely touched:** all `Controllers/*.cs`, all `Dtos/**/*.cs`
**Estimated scope:** M

---

##### Task 19c: VPS deployment guide
**Description:** Section in README (or `docs/deploy.md`) covering VPS deployment with Docker, reverse proxy (Caddy or Nginx), and HTTPS via Let's Encrypt.

**Acceptance criteria:**
- [ ] Sample `docker-compose.prod.yml` referenced
- [ ] Caddyfile example with automatic HTTPS
- [ ] Steps: provision VPS, point domain, install Docker, clone, env, `docker compose up -d`
- [ ] Notes on backups (Postgres volume) and updates (`git pull && docker compose up -d --build`)

**Verification:**
- [ ] Guide is internally reviewed and tested on a fresh VPS
- [ ] Commands all run without modification

**Dependencies:** 20c
**Files likely touched:** `README.md` or `docs/deploy.md`
**Estimated scope:** M

---

#### Task 20: Docker Production Build

##### Task 20a: Multi-stage Dockerfile for API
**Description:** Production Dockerfile that builds and publishes a self-contained API image.

**Acceptance criteria:**
- [ ] Stages: `sdk` (build + publish) → `aspnet` (runtime)
- [ ] Non-root user (`app`)
- [ ] Healthcheck via `curl` to `/health`
- [ ] Final image ≤ 250 MB

**Verification:**
- [ ] `docker build -t api:dev backend` succeeds
- [ ] `docker run --rm -p 5000:8080 api:dev` starts and `/health` returns 200
- [ ] `docker images api:dev` reports size ≤ 250 MB

**Dependencies:** 1d
**Files likely touched:** `backend/Dockerfile`
**Estimated scope:** S

---

##### Task 20b: Multi-stage Dockerfile for frontend
**Description:** Production Dockerfile that builds the Vite app and serves it with Nginx.

**Acceptance criteria:**
- [ ] Stages: `node` (build) → `nginx` (serve `dist/`)
- [ ] Custom `nginx.conf` proxies `/api` to the API service
- [ ] Gzip + cache headers for static assets
- [ ] Final image ≤ 50 MB (excluding assets)

**Verification:**
- [ ] `docker build -t web:dev frontend` succeeds
- [ ] `docker run --rm -p 8080:80 web:dev` serves the SPA and `/api/health` proxies to backend (requires backend running)

**Dependencies:** 2a
**Files likely touched:** `frontend/Dockerfile`, `frontend/nginx.conf`
**Estimated scope:** S

---

##### Task 20c: Production docker-compose + env config
**Description:** `docker-compose.prod.yml` with API, web, and Postgres services plus `.env.example` documenting required variables.

**Acceptance criteria:**
- [ ] `docker-compose.prod.yml` references both Dockerfiles and uses environment variables
- [ ] `.env.example` lists `POSTGRES_PASSWORD`, `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `VITE_API_URL`
- [ ] Includes named volume `pgdata` for Postgres
- [ ] Web service depends on API healthcheck
- [ ] `docker compose -f docker-compose.prod.yml --env-file .env up -d --build` works end-to-end

**Verification:**
- [ ] Full app reachable at `http://localhost` (web on 80) and API at `:5000`
- [ ] `.env.example` is the only env reference committed; secrets stay out of git

**Dependencies:** 20a, 20b
**Files likely touched:** `docker-compose.prod.yml`, `.env.example`, `.gitignore`
**Estimated scope:** M

---

#### Checkpoint: Complete
- [ ] `dotnet test` returns 0
- [ ] `npm run build` returns 0
- [ ] `docker compose -f docker-compose.prod.yml up` brings up the full stack
- [ ] Customer and admin flows tested end-to-end via Playwright (or manual checklist)
- [ ] README + Swagger cover the full surface

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| ASP.NET Core Identity + EF Core + PostgreSQL configuration | High | Test early in 3a with docker-compose, verify connection string |
| JWT auth between SPA and API (CORS, token refresh) | Medium | Configure CORS in 1d, store token in Zustand + localStorage, add refresh in a later phase |
| Image upload handling (local path, serving) | Medium | Use `IImageStorage` interface (4c), serve via static files middleware |
| shadcn/ui + Tailwind config conflicts | Low | Follow shadcn init guide carefully in 2b |
| Front-end state complexity (cart + auth) | Medium | Use Zustand for client state (auth), TanStack Query for server state (cart, products, orders) |
| Stock race conditions during checkout | Medium | Wrap stock validation + deduction in a DB transaction (11a) |
| Hard-delete of products in use | Medium | Block hard delete when referenced (13b) |
| Test flakiness with shared DB | Medium | Testcontainers per test class in 18a |

---

## Open Questions
- [x] Image Storage -> Local with `IImageStorage` interface ✅
- [x] Payment -> Mock checkout with `IPaymentService` interface ✅
- [x] Docker/VPS -> docker-compose for dev and prod ✅
- [x] UI Library -> Tailwind CSS + shadcn/ui ✅
- [x] Email -> Out of scope, architecture allows later addition ✅
- [ ] Token refresh strategy: silent refresh vs re-login? (defer; current scope is login-only)
- [ ] Image CDN signing for production? (defer; out of scope)
