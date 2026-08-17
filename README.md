# Mini E-Commerce Platform

A full-stack e-commerce application with a customer-facing storefront and an admin panel. Built with React + TanStack Query + Zustand on the frontend, ASP.NET Core Web API on the backend, and PostgreSQL for storage.

## Features

**Customer**
- Register / login with JWT auth and role-based access
- Product catalog with search, category filter, sorting, pagination, and product detail pages
- Persistent cart with live updates
- Checkout with stock re-validation and a mock payment provider (with configurable failure modes for testing)
- Order confirmation and order history

**Admin**
- Product management: CRUD + image upload
- Order management: list, detail, and guarded status transitions (cancelling restocks items)
- Dashboard: KPI cards, sales chart, recent orders, low-stock table

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | React 19, TypeScript, Vite, Tailwind CSS + shadcn/ui, TanStack Query, Zustand, React Router, Axios, Recharts |
| Backend | ASP.NET Core (.NET 10), EF Core + Npgsql, ASP.NET Core Identity, JWT, ImageSharp, Swashbuckle |
| Database | PostgreSQL 16 |
| Infrastructure | Docker + docker-compose, GitHub Actions CI |

## Repository layout

```
├── backend/
│   └── MiniEcommerce.Api/            # ASP.NET Core Web API
│       ├── Controllers/              # REST endpoints (api/* routes)
│       ├── Data/                     # DbContext + migrations + seed
│       ├── Dtos/                     # Request/response DTOs
│       ├── Models/                   # EF entities
│       ├── Services/                 # image storage, payment, etc.
│       └── Repositories/             # generic IRepository<T>
│   └── MiniEcommerce.Api.Tests/      # xUnit (unit + integration)
├── frontend/                         # React + Vite SPA
│   └── src/
│       ├── components/               # shared + admin components
│       ├── pages/                    # route pages
│       ├── lib/                      # api client, hooks, stores, types
│       └── test/                     # MSW server + handlers + setup
├── docs/
│   ├── testing.md                    # how to run/write tests (day-to-day guide)
│   └── adr/                          # architecture decision records
├── tasks/                            # plan.md + todo.md (source-of-truth plan)
├── docker-compose.yml                # local dev: PostgreSQL + API
└── CONTEXT.md                        # domain glossary + cross-cutting rules
```

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (backend)
- [Node.js 22+](https://nodejs.org/) and npm (frontend)
- [Docker](https://www.docker.com/products/docker-desktop/) with Docker Compose (for the quick start and the local database)

## Quick start (Docker)

The simplest path — brings up PostgreSQL and the API:

```bash
docker compose up --build
```

- API: <http://localhost:5000> (Swagger at <http://localhost:5000/swagger>)
- Health: `curl http://localhost:5000/health` → `{"status":"ok",...}`
- PostgreSQL: `localhost:5432` (`mini_ecommerce` / `postgres` / `postgres`)

Migrations run automatically on startup and seed data is applied idempotently.

## Manual dev setup

Run the database, then the API, then the frontend — each in its own terminal.

**1. Database**

```bash
docker compose up -d db
```

**2. Backend API** (listens on `http://localhost:5000`)

```bash
cd backend
dotnet run --project MiniEcommerce.Api
```

**3. Frontend** (listens on `http://localhost:5173`, proxies `/api/*` → `http://localhost:5000`)

```bash
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. The Vite dev server forwards `/api/*` to the backend unchanged (the API exposes its routes under `/api`, e.g. `/api/products`, `/api/auth/login`).

> The frontend axios client uses `import.meta.env.VITE_API_URL || '/api'` as its base URL. In dev this resolves to the Vite proxy; in production you can point it at the API origin via the `VITE_API_URL` build-time env var.

## Seed accounts

| Role | Email | Password |
|---|---|---|
| Admin | `admin@example.com` | `Admin123!` |
| Customer | `customer@example.com` | `Customer123!` |

Seeded catalog: 5 categories, 20 products. Seed is idempotent — re-running does not duplicate rows.

## Testing

Both suites must be green before every commit (CI enforces this — see `.github/workflows/ci.yml`).

```bash
# Backend (xUnit — 165 tests)
cd backend
dotnet test

# Frontend (Vitest + RTL + MSW — 205 tests)
cd frontend
npm run test:run          # one-shot (CI mode)
npm test                  # watch mode
npm run test:coverage     # coverage report
```

The practical day-to-day guide is in [`docs/testing.md`](docs/testing.md); the full test strategy (pyramid, conventions, tooling decisions) is in [`tasks/test-spec.md`](tasks/test-spec.md).

## API surface (summary)

All endpoints wrap responses in the `ApiResponse<T>` envelope and use machine-readable error codes. Full interactive docs are in Swagger at `/swagger` (dev).

| Area | Routes |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Catalog | `GET /api/products`, `GET /api/products/{id}` |
| Cart | `GET /api/cart`, `POST /api/cart/items`, `PUT/DELETE /api/cart/items/{id}`, `DELETE /api/cart` |
| Orders | `POST /api/orders`, `GET /api/orders`, `GET /api/orders/{id}` |
| Payments | `GET /api/payments/mock-mode` |
| Admin products | `GET/POST /api/admin/products`, `PUT/DELETE /api/admin/products/{id}`, `POST /api/admin/products/{id}/images`, `DELETE /api/admin/products/{id}/images/{imageId}` |
| Admin orders | `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `PUT /api/admin/orders/{id}/status` |
| Admin dashboard | `GET /api/admin/dashboard/summary`, `/sales`, `/recent-orders`, `/low-stock` |
| Health | `GET /health`, `GET /api/health` |

Admin endpoints require a JWT with the `Admin` role; cart/order endpoints require an authenticated `Customer`.

## Project documentation

- [`CONTEXT.md`](CONTEXT.md) — the canonical domain glossary and cross-cutting rules (response envelope, role gating, decimal money, UTC timestamps, snapshot semantics). Read this before touching domain code.
- [`docs/adr/`](docs/adr/) — architecture decision records (cancellation policy, stock handling, payment failure modes, etc.).
- [`tasks/plan.md`](tasks/plan.md) — the implementation plan with acceptance criteria per task.
- [`tasks/todo.md`](tasks/todo.md) — the granular working checklist.

## Production deployment

A production deployment guide (VPS) and a production Docker build (`docker-compose.prod.yml`, multi-stage frontend image) are part of the current work in [`tasks/todo.md`](tasks/todo.md) — Tasks 21–22. Production hardening notes (JWT key, CORS origins, mock payment mode) are documented in `appsettings.json` and the ADRs.
