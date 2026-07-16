# 15a — Admin order list endpoint

**What to build:** `GET /api/admin/orders` returns a paginated, filterable list of every order in the system, scoped to any caller with `Role = Admin`. The admin can scan all customers' orders, filter by status / date range / free-text search, and page through the results. The list response carries only the fields the admin table renders (no full detail leakage) but includes everything the table needs: id, customer email, status, total, item count, created-at.

**Parent:** #5

**Blocked by:** None — can start immediately.

**Status:** ready-for-agent

- [ ] `AdminOrdersController` exists at `api/admin/orders` with `[Authorize(Roles = "Admin")]` at the class level
- [ ] `GET /api/admin/orders` accepts `page` (default 1, clamped >= 1), `pageSize` (default 20, clamped [1, 100]), `status?` (exact match on the `OrderStatus` enum string), `q?` (free-text; matches customer email via case-insensitive `Contains` OR order id as a numeric string), `from?` and `to?` (ISO-8601 dates; `to` is exclusive at the day level so the window is inclusive of both calendar days)
- [ ] Returns `200` with `ApiResponse<List<AdminOrderListItem>>` and `Meta { page, pageSize, totalCount }`
- [ ] `AdminOrderListItem` exposes `id`, `customerId`, `customerEmail`, `status` (string), `total` (decimal), `itemCount` (int — sum of quantities), `createdAt` (UTC)
- [ ] Ordering is `OrderByDescending(CreatedAt).ThenByDescending(Id)` for stable tie-break
- [ ] Returns `401` when no JWT is present, `403` when a Customer JWT is present
- [ ] `400 INVALID_STATUS` if the `status` query string is not a known `OrderStatus` value
- [ ] Integration tests via `WebApplicationFactory<Program>` cover the 9 cases listed in the spec under "GetOrders integration tests" (role gating, returns all across customers, status filter, date range, email search, id search, pagination meta, newest-first ordering)
- [ ] `dotnet test` green for the new test class; no regressions in the existing suite
