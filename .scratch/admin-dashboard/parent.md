## Problem Statement

The Admin role can manage products (Tasks 13–14) and orders (Tasks 15–16) through the API and UI, but has **no operational overview**. There is no single screen that answers "how is the store doing right now?" — total orders, revenue, customer count, low-stock alerts, sales trend, and recent activity. The `/admin` landing page is a placeholder ("Dashboard coming soon..."). An admin must click into Products and Orders and mentally aggregate to answer basic questions.

## User Stories

### 17a — Dashboard stats endpoints

1. As an **Admin**, I want a summary endpoint returning total orders, total revenue, total customers, total products, and low-stock count, so that I can render KPI cards without N round-trips.
2. As an **Admin**, I want a daily sales series endpoint (`?days=30`), so that I can render a sales trend chart.
3. As an **Admin**, I want a recent-orders endpoint (`?limit=10`), so that I can see the latest activity at a glance.
4. As an **Admin**, I want a low-stock endpoint (`?threshold=10`), so that I can spot products that need restocking.
5. As an **Admin**, I want every dashboard endpoint to require `Role = Admin`, so that customers cannot read store-wide metrics.

### 17b — Dashboard cards UI

6. As an **Admin**, I want the `/admin` landing page to show 4 KPI cards (orders, revenue, customers, low stock) in a responsive grid, so that I get the headline numbers at a glance.
7. As an **Admin**, I want a loading skeleton while the summary is in flight, so that the layout does not jump.

### 17c — Charts

8. As an **Admin**, I want a 30-day sales line chart, so that I can see the revenue trend.
9. As an **Admin**, I want a recent-orders list (10 rows) with status badges linking to the order detail page, so that I can jump into fulfilment.
10. As an **Admin**, I want a low-stock table (product name, stock, Edit link), so that I can restock from the dashboard.
11. As an **Admin**, I want loading + empty states on every section, so that the dashboard degrades gracefully.

## Implementation Decisions

- **Charting:** Recharts (per plan.md 17c). It is **not** currently installed — Ticket 17.2 adds the dependency.
- **Backend structure:** follow the repo convention — a flat `AdminDashboardController` using `ApplicationDbContext` directly (no `Services/DashboardService.cs`; plan.md's `Controllers/Admin/` path is stale). Role gating per action via `[Authorize(Roles = "Admin")]` (CONTEXT.md rule #4).
- **Response envelope:** every endpoint returns `ApiResponse<T>` (CONTEXT.md rule #5).
- **Money:** revenue is `decimal`, summed from `Order.Total` (CONTEXT.md rule #7).
- **Timestamps:** UTC on write; display layer formats to local (CONTEXT.md rule #8).
- **Snapshot is the truth:** the recent-orders list reuses the existing `AdminOrderListItem` shape (id, customerEmail, status, total, createdAt) — no new order DTO.
- **Low stock:** `Stock <= threshold` on `Product` (v1 has no variants yet — ADR 0003 is Phase 7).
- **Slicing:** two vertical tickets — 17.1 (summary endpoint + KPI cards), 17.2 (sales + recent orders + low stock endpoints + charts UI). 17.2 is blocked by 17.1 because both edit the same `Dashboard.tsx` page (plan.md same-page rule → sequential).

## Modules touched (per plan.md files lists, adjusted to repo conventions)

- **17.1** — `AdminDashboardController` (new, summary action), dashboard DTOs (new, `DashboardSummary`), `frontend/src/pages/admin/Dashboard.tsx` (replaces placeholder body), `frontend/src/components/admin/StatCard.tsx` (new), `useAdminDashboard` hook (new)
- **17.2** — `AdminDashboardController` (adds sales / recent-orders / low-stock actions), dashboard DTOs (adds `SalesPoint` + recent-order / low-stock DTOs), `frontend/src/components/admin/SalesChart.tsx` (new), `RecentOrdersList.tsx` (new), `LowStockTable.tsx` (new), `Dashboard.tsx` (adds sections), `package.json` (adds recharts)
