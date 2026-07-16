# Admin Order Management API (Tasks 15a–c)

> Spec produced by `to-spec` (2026-07-13). Source of truth for the implementation tickets that follow. Apply `ready-for-agent` label on issue creation.

---

## Problem Statement

Admins have no way to see or act on customer orders through the API. The customer-facing `OrdersController` exposes `GET /api/orders` and `GET /api/orders/:id`, but it scopes every query to the JWT's `CustomerId` and there is no `PUT` for status changes at all. Operators running the storefront have no list view, no cross-customer detail view, and no way to advance an order through `Pending → Paid → Shipped → Delivered` or to cancel one — the entire admin flow for order fulfilment is missing. Without it the storefront's "checkout" path ends in a row nobody can act on.

## Solution

Add a new `AdminOrdersController` mounted at `/api/admin/orders` that:

1. Lists every order in the system (across all customers) with filterable, paginated queries by status, free-text search, and date range.
2. Returns the full detail of any order — including the customer identity, items, shipping snapshot, and computed item subtotals.
3. Exposes a single `PUT /api/admin/orders/:id/status` endpoint that walks an order through its lifecycle, enforces a transition guard rail, and restocks items on `Cancelled` regardless of prior state.

The controller inherits the existing admin auth pattern (`[Authorize(Roles = "Admin")]`) and the existing `ApiResponse<T>` envelope. The customer-facing `OrdersController` is unchanged. No new tables; no migration.

## User Stories

1. As an **Admin**, I want to list every order in the system, so that I can see what customers have bought.
2. As an **Admin**, I want the order list paginated (default 20, max 100), so that the page is usable when there are thousands of orders.
3. As an **Admin**, I want to filter the order list by `status` (e.g. `Paid`), so that I can focus on the orders that need fulfilment work right now.
4. As an **Admin**, I want to filter the order list by a date range (`from`, `to`), so that I can review a specific day's orders or a weekly batch.
5. As an **Admin**, I want a free-text search that matches the customer's email or the order id, so that I can jump straight to a specific customer or order.
6. As an **Admin**, I want each list row to show the customer email, current status, total, and created-at timestamp, so that I can scan the list without clicking into each one.
7. As an **Admin**, I want to click into any order and see its full detail (customer email + full name, items with snapshot product names and unit prices, shipping address, totals, status, created-at), so that I can review a specific order end-to-end.
8. As an **Admin**, I want a 404 if I request an order that does not exist, so that the UI can render a clear "not found" state instead of crashing.
9. As an **Admin**, I want to advance an order from `Pending` to `Paid` (e.g. when the payment provider's webhook is delayed), so that fulfilment is not blocked by infrastructure.
10. As an **Admin**, I want to advance an order from `Paid` to `Shipped` once it leaves the warehouse, so that the customer is told their order is on its way.
11. As an **Admin**, I want to advance an order from `Shipped` to `Delivered` once the customer confirms receipt, so that the lifecycle is closed.
12. As an **Admin**, I want to cancel any non-terminal order (`Pending`, `Paid`, `Shipped`), so that mistakes, fraud, or out-of-stock cases are recoverable.
13. As an **Admin**, I want cancellation to **always** restock the order's items to `Product.Stock`, so that the units become available to other customers again — even for `Pending` orders (per ADR 0001: the live `OrdersController.Checkout` deducts stock in-memory before `SaveChanges`, so every persisted order has stock to restock; there is no Pending exemption).
14. As an **Admin**, I want an invalid status transition to return `409 INVALID_TRANSITION` with a machine-readable code, so that the UI can show a precise error and the API contract is unambiguous.
15. As an **Admin**, I want `Delivered` and `Cancelled` to be terminal — once an order is in either, no further transitions are allowed, so that the lifecycle cannot be reopened by mistake.
16. As an **Admin**, I want a `200 OK` with the updated order when a transition succeeds, so that the UI can update its state without a follow-up fetch.
17. As an **Admin**, I want every admin-order endpoint to require `Role = Admin`, so that a customer with a valid JWT cannot enumerate or mutate orders they do not own.
18. As an **Admin**, I want a 401 (no token) and a 403 (Customer token) to be clearly distinguished, so that the UI can react appropriately.
19. As a **Customer**, I want my customer-facing order endpoints to remain unchanged, so that admin work does not regress the storefront.
20. As an **integrator of the admin UI** (Task 16), I want the list endpoint's response shape to include only the fields the table renders (no leakage of the full detail), so that the wire payload stays small.
21. As an **integrator of the admin UI** (Task 16b), I want the detail endpoint to compute a per-line `subtotal` for each item, so that I do not have to duplicate the multiplication in the UI.
22. As a **developer of future work** (Task 17 dashboard), I want the order list endpoint to accept the same `status` filter the dashboard will use, so that the dashboard can reuse the contract.

## Implementation Decisions

### Modules

- **New: `AdminOrdersController`** mounted at `api/admin/orders`, attribute-routed, gated by `[Authorize(Roles = "Admin")]` at the class level. Three actions: `GetOrders`, `GetOrderById`, `UpdateOrderStatus`.
- **New: `AdminOrderDtos`** (request + response DTOs) in `Dtos/`. Pattern matches the existing `AdminProductDtos` file.
- **New: status-transition guard** — a static `OrderStatusTransitions` helper (or method on the controller) that returns the set of allowed next statuses for the current status, or an empty set for terminal states. This is the single source of truth for the state machine and is exercised by both the controller and the tests.
- **No service layer.** The controller talks directly to `ApplicationDbContext` (same pattern as `AdminProductsController` and the customer `OrdersController`). Adding a service here would be a horizontal-layer cut with no new capability — the controller is already the highest seam and the only one that needs the HTTP-boundary tests.
- **No migration.** No new entities, no new columns. We read what already exists.

### Auth

- `[Authorize(Roles = "Admin")]` at the class level. The existing `Jwt:RoleClaimType = ClaimTypes.Role` setup in `Program.cs` is unchanged; tests rely on the same wiring the live API uses (already verified by `AuthControllerTests` and `AdminProductsControllerTests`).
- Customer JWTs return `403`; missing JWTs return `401`. Both are asserted explicitly.

### State machine

| From        | Allowed targets              |
|-------------|------------------------------|
| `Pending`   | `Paid`, `Cancelled`          |
| `Paid`      | `Shipped`, `Cancelled`       |
| `Shipped`   | `Delivered`, `Cancelled`     |
| `Delivered` | _(terminal — none)_          |
| `Cancelled` | _(terminal — none)_          |

- `Delivered` and `Cancelled` are terminal. The state machine is enforced server-side; the client cannot request a transition out of a terminal state.
- The state machine is implemented as a `switch` over the current status, returning either a `HashSet<OrderStatus>` of allowed nexts or an empty set for terminal. This is testable as a pure function (no DB) and is the one piece of logic worth a dedicated unit test in addition to the integration test.

### API contract — list

```
GET /api/admin/orders
  Query: page (default 1, clamped to >=1)
         pageSize (default 20, clamped to [1, 100])
         status? (Pending|Paid|Shipped|Delivered|Cancelled — exact match)
         q? (free-text; matches customer email OR order id as a numeric string)
         from? (ISO-8601 date, inclusive)
         to?   (ISO-8601 date, inclusive; combined with from as [from, to+1day) to make the day inclusive)
  200 → ApiResponse<List<AdminOrderListItem>> with Meta { page, pageSize, totalCount }
  401 if no token
  403 if Customer role
```

`AdminOrderListItem` fields: `id`, `customerId`, `customerEmail`, `status` (string), `total` (decimal), `itemCount` (int — sum of quantities), `createdAt` (UTC).

Ordering: `OrderByDescending(o => o.CreatedAt)` then `ThenByDescending(o => o.Id)` for stable tie-break.

### API contract — detail

```
GET /api/admin/orders/{id:int}
  200 → ApiResponse<AdminOrderDetail>     (admin sees any order)
  404 ORDER_NOT_FOUND if not found
  401 / 403 as above
```

`AdminOrderDetail` fields: every `Order` column (id, status, subtotal, shippingFee, total, all `Shipping*` fields, createdAt) **plus** `customer { id, email, fullName }` **plus** `items: [ { id, productId, productName, unitPrice, quantity, subtotal } ]`.

### API contract — status update

```
PUT /api/admin/orders/{id:int}/status
  Body: { "status": "Shipped" }            — UpdateOrderStatusRequest
  200 → ApiResponse<AdminOrderDetail>      (echoes the updated order)
  404 ORDER_NOT_FOUND
  409 INVALID_TRANSITION                   (current → requested not in the table)
  409 ORDER_ALREADY_TERMINAL               (current is Delivered or Cancelled)
  400 VALIDATION_ERROR                     (body missing `status`, or status not a known enum value)
  401 / 403 as above
```

The body validation uses a `DataAnnotations` `[Required]` on `Status` plus a custom validation attribute (or a small `IValidatableObject` on the request) that rejects any status string not in the `OrderStatus` enum. This is what produces the `400 VALIDATION_ERROR` for "unknown status strings" — without it, ASP.NET Core's enum binding would happily parse `"Banana"` into `(OrderStatus)0` and the guard rail would see `Pending`, which is wrong.

### Restock on cancel

When the requested transition is `→ Cancelled`, the controller:

1. Loads the order with its `Items` (and the related `Product` for each item — `Include` chain `Items → Product`).
2. For each `OrderItem`, increments `Product.Stock` by `OrderItem.Quantity` in-memory.
3. Sets `Order.Status = Cancelled`.
4. Calls `SaveChangesAsync`.

This is restock-always. There is no `Pending` exemption. ADR 0001 is the source of truth; the 2026-07-13 grill explicitly removed the no-op exception because the live checkout deducts stock in-memory before `SaveChanges`, so every persisted order has stock to restock. A test asserts: create a `Pending` order via the customer flow, cancel it as Admin, and verify the product's stock returned to its pre-checkout value.

### What we do NOT do

- No `CreatedAt`-as-default UTC handling change. `DateTime.UtcNow` is already the convention (see `CONTEXT.md` cross-cutting rule 8).
- No background job for abandonment. That's v2 (ADR 0007).
- No refund flow. v1 has no real provider; cancellation is a stock-restock only.
- No soft delete on orders. Orders are immutable history.
- No bulk transitions, no multi-select, no CSV export. Out of scope.
- No email / notification side effects. Out of scope per `CONTEXT.md`.

## Testing Decisions

### What makes a good test

- Assert on **the HTTP response** (status code, response envelope, response body fields). Do not assert on the EF context internals.
- One assertion concept per test, but multiple `.Should()` calls on the same object are fine (e.g. `body.Data!.Id.Should().Be(orderId); body.Data.Status.Should().Be("Paid")`).
- Each test owns its DB state via the existing `ResetDatabaseAsync` + `SeedCatalogDataAsync` pattern in `DbContextExtensions`. No static state, no `Thread.Sleep`, no shared mutable fields.
- Token issuance uses the existing helper methods in the test files (`RegisterAndLoginAsync`, `CreateAdminAndLoginAsync`); copy the pattern from `AdminProductsControllerTests` / `OrdersControllerTests`.

### Which modules are tested

- **`AdminOrdersController` end-to-end** via `WebApplicationFactory<Program>` + the in-memory DB configured in `ApiFactory`. This is the canonical seam — one seam, highest fidelity, no test-doubles. The `to-spec` seam review confirmed this.
- **`OrderStatusTransitions` state machine** as a pure-function unit test (no DB, no HTTP). Two reasons: (a) it has nontrivial logic worth pinning down with a table-driven test that enumerates every (current, requested) pair; (b) it gives the integration tests a vocabulary to assert against without re-asserting the entire transition table.

### Prior art in this repo

- `Integration/Controllers/AdminProductsControllerTests.cs` — the structure to copy. It demonstrates the per-test factory, the `IAsyncLifetime` init, the role-gating tests, the JSON deserialization against `ApiResponse<T>`, and the `Json` static with `CamelCase`.
- `Integration/Controllers/OrdersControllerTests.cs` — for the order-specific helpers (`RegisterAndLoginAsync`, `ValidCheckoutRequest`, `AddCartItemRequest`).
- `Infrastructure/ApiFactory.cs` — the test host. It already pre-configures the 64-byte JWT key, the in-memory DB, the temp `IImageStorage`, and the `MockPaymentService` mode holder. Nothing about the factory changes.
- `Infrastructure/DbContextExtensions.cs` — `ResetDatabaseAsync` already deletes `Orders` and `OrderItems` first, so admin-order tests get a clean slate.

### Required test cases (for the implementer)

**`OrderStatusTransitions` unit tests**

- `AllowedNexts_Pending_Returns_Paid_And_Cancelled`
- `AllowedNexts_Paid_Returns_Shipped_And_Cancelled`
- `AllowedNexts_Shipped_Returns_Delivered_And_Cancelled`
- `AllowedNexts_Delivered_Returns_Empty`
- `AllowedNexts_Cancelled_Returns_Empty`
- `CanTransition_Delivered_To_Anything_Returns_False`
- `CanTransition_Cancelled_To_Anything_Returns_False`

**`AdminOrdersController.GetOrders` integration tests**

- `GetAdminOrders_WithoutToken_Returns401`
- `GetAdminOrders_WithCustomerToken_Returns403`
- `GetAdminOrders_WithAdminToken_ReturnsAllOrdersAcrossCustomers`
- `GetAdminOrders_FilterByStatus_ReturnsOnlyMatchingOrders`
- `GetAdminOrders_FilterByDateRange_NarrowsResults`
- `GetAdminOrders_SearchByEmail_FindsCustomerOrder`
- `GetAdminOrders_SearchByOrderId_FindsOrder`
- `GetAdminOrders_Pagination_MetaIsCorrect`
- `GetAdminOrders_OrderedNewestFirst`

**`AdminOrdersController.GetOrderById` integration tests**

- `GetAdminOrderById_WithoutToken_Returns401`
- `GetAdminOrderById_WithCustomerToken_Returns403` (cross-tenant — customer cannot read another customer's order via admin endpoint, even with a Customer token)
- `GetAdminOrderById_WithAdminToken_ReturnsFullDetail`
- `GetAdminOrderById_AdminOrderDetail_HasExpectedFields` (customer email, full name, items, shipping, totals, computed item subtotals)
- `GetAdminOrderById_WhenOrderDoesNotExist_Returns404`

**`AdminOrdersController.UpdateOrderStatus` integration tests**

- `UpdateOrderStatus_WithoutToken_Returns401`
- `UpdateOrderStatus_WithCustomerToken_Returns403`
- `UpdateOrderStatus_PendingToPaid_Returns200AndPersists`
- `UpdateOrderStatus_PaidToShipped_Returns200AndPersists`
- `UpdateOrderStatus_ShippedToDelivered_Returns200AndPersists`
- `UpdateOrderStatus_PendingToCancelled_RestocksItems` ← the restock-always behavior
- `UpdateOrderStatus_PaidToCancelled_RestocksItems`
- `UpdateOrderStatus_ShippedToCancelled_RestocksItems`
- `UpdateOrderStatus_DeliveredToAnything_Returns409InvalidTransition`
- `UpdateOrderStatus_CancelledToAnything_Returns409InvalidTransition`
- `UpdateOrderStatus_PendingToDelivered_Returns409InvalidTransition` (skipping a step)
- `UpdateOrderStatus_UnknownStatusString_Returns400ValidationError`
- `UpdateOrderStatus_MissingStatusField_Returns400ValidationError`
- `UpdateOrderStatus_WhenOrderDoesNotExist_Returns404`
- `UpdateOrderStatus_AfterCancel_OrderIsTerminal` (cannot cancel twice)

## Out of Scope

- UI (covered by `Task 16` in `plan.md`).
- Dashboard endpoints and stats (covered by `Task 17`).
- Product variants on the admin order view (covered by `Task 27`).
- Address book (covered by `Task 26`).
- Refund / chargeback — requires a real payment provider.
- Customer self-cancel — explicitly out of scope per `CONTEXT.md` and ADR 0001.
- Abandonment / auto-expiry of `Pending` orders — Phase 8 (ADR 0007).
- Audit log of who changed which status when — out of scope for v1; add when the role-claim setup evolves.
- Sorting options other than newest-first — single sort keeps the contract small.
- Filtering by product, by customer name, or by total amount — keep the filter set minimal; add as the UI surfaces demand.

## Further Notes

- The restock behavior in 15c intentionally mirrors the existing `OrdersController.Checkout` pattern of in-memory mutation + `SaveChanges`. The atomic-SQL-UPDATE path (ADR 0002) is rolled back in v1; reservations (ADR 0007) replace both in v2. Do not introduce a third "stock mutation" pattern here.
- The `Status` field on the response is a **string**, not the enum integer. This matches the customer `OrderDto` convention. The frontend uses a status badge component already (added in Task 12c); the admin UI can reuse it.
- The `q` search is intentionally simple: case-insensitive `Contains` on the customer email and a numeric `id` parse. No fuzzy matching, no full-text index. Postgres `ILIKE` would be the production-grade move, but the in-memory provider doesn't support it — for the v1 in-memory tests, the LINQ `Contains` is fine, and a production note in the code comments can flag the migration story.
- Date-range semantics: `from` and `to` are both date-only inputs interpreted as UTC midnight. `to` is exclusive at the day level (i.e. `to = 2026-07-13` means "before 2026-07-14 00:00 UTC"), so the `from..to` window is inclusive of both calendar days. Documented in the Swagger XML on the controller action.
- This spec deliberately does **not** add a service. If the future Tasks 17 (dashboard) or a future "bulk ship" feature need to share transition logic, the `OrderStatusTransitions` helper is already the extraction point — promote it to a service at that time, not before.
