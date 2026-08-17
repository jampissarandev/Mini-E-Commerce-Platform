## Parent

Part of #PARENT (Task 17 — Admin Dashboard).

## What to build

Admin opens /admin and sees 4 KPI cards (total orders, total revenue, total customers, low-stock count) with real numbers from a new `GET /admin/dashboard/summary` endpoint. Loading skeleton while the summary is in flight; graceful empty state. Auth gating works (admins only; customers get 403, anonymous redirected to login). The bare-bones Dashboard placeholder is replaced with a real cards grid.

## Acceptance criteria

- [ ] `GET /admin/dashboard/summary` returns `ApiResponse<DashboardSummary>` with `{ totalOrders, totalRevenue, totalCustomers, totalProducts, lowStockCount }`; every number matches a direct DB query
- [ ] Endpoint requires `Role = Admin` (401 with no token, 403 with a Customer token)
- [ ] `/admin` renders 4 KPI cards in a responsive grid: orders, revenue, customers, low-stock count
- [ ] Loading skeleton while the summary request is in flight (layout does not jump)
- [ ] `useAdminDashboard()` TanStack Query hook with a stable query key (e.g. `['admin-dashboard-summary']`)
- [ ] Backend integration tests (role gating + numbers match DB) and frontend tests (cards render correct values) are green; no regressions in the existing suite

## Blocked by

- None — can start immediately.

## Out of scope (deferred to Ticket 17.2)

- Sales chart, recent-orders list, low-stock table
