## Parent

Part of #PARENT (Task 17 — Admin Dashboard).

## What to build

The dashboard gains three data sections below the KPI cards: a 30-day sales line chart (Recharts), a recent-orders list (10 rows with status badges linking to `/admin/orders/:id`), and a low-stock products table (name, stock, Edit link to `/admin/products`). Each section has loading + empty states. Backed by three new endpoints: `GET /admin/dashboard/sales?days=30`, `GET /admin/dashboard/recent-orders?limit=10`, `GET /admin/dashboard/low-stock?threshold=10`.

## Acceptance criteria

- [ ] `GET /admin/dashboard/sales?days=30` returns `ApiResponse<List<SalesPoint>>` with `[{ date, total, orderCount }]` — exactly `days` points, one per calendar day, totals matching direct DB queries
- [ ] `GET /admin/dashboard/recent-orders?limit=10` returns the latest orders (newest-first), reusing the existing `AdminOrderListItem` shape
- [ ] `GET /admin/dashboard/low-stock?threshold=10` returns products with `Stock <= threshold` (id, name, stock)
- [ ] All three endpoints require `Role = Admin` (401 / 403)
- [ ] Sales line chart renders with Recharts (dependency added to `package.json`); loading + empty states
- [ ] Recent-orders list: 10 rows, status badges, rows link to `/admin/orders/:id`
- [ ] Low-stock table: product name, stock, Edit link to `/admin/products`
- [ ] Backend integration tests + frontend tests green; no regressions in the existing suite

## Blocked by

- #BLOCKER (Ticket 17.1 — summary endpoint + KPI cards) — the dashboard page and its query-key conventions land there first; both tickets edit the same page so they run sequentially.

## Out of scope

- KPI cards (Ticket 17.1)
