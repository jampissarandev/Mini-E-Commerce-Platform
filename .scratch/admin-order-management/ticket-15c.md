# 15c — Admin order status transitions + restock on cancel

**What to build:** `PUT /api/admin/orders/:id/status` advances an order through its lifecycle. The endpoint enforces a server-side state machine (`Pending → {Paid, Cancelled}`, `Paid → {Shipped, Cancelled}`, `Shipped → {Delivered, Cancelled}`, `Delivered` and `Cancelled` terminal). Cancelling from any non-terminal state **always** restocks the order's items to `Product.Stock` — there is no Pending exemption (per ADR 0001, since the live checkout deducts stock in-memory before `SaveChanges`).

**Parent:** #5

**Blocked by:** #15b (the status-update response reuses the `AdminOrderDetail` shape from 15b)

**Status:** ready-for-agent

- [ ] `OrderStatusTransitions` static helper: `IReadOnlySet<OrderStatus> AllowedNexts(OrderStatus current)` — pure function, no DB. Returns the allowed next statuses per the state machine table; returns an empty set for terminal states. Tested as a pure-function unit test (no DB, no HTTP).
- [ ] `PUT /api/admin/orders/{id:int}/status` accepts `UpdateOrderStatusRequest { Status: string }`
- [ ] Request validation: `[Required]` on `Status` plus an `IValidatableObject` (or custom `ValidationAttribute`) that rejects any string not in the `OrderStatus` enum — produces `400 VALIDATION_ERROR` for "Banana" and for a missing field
- [ ] State machine enforcement via `OrderStatusTransitions.AllowedNexts(order.Status)`
- [ ] On any allowed transition: set `Order.Status = requested`, `SaveChangesAsync`, return `200` with `ApiResponse<AdminOrderDetail>` (echoes the updated order using the shape from 15b)
- [ ] On disallowed transition from a non-terminal state: return `409 INVALID_TRANSITION`
- [ ] On disallowed transition from a terminal state: return `409 ORDER_ALREADY_TERMINAL`
- [ ] On transition `→ Cancelled`: load `Order.Items` with `Include(Items).ThenInclude(Product)`, increment `Product.Stock` by `OrderItem.Quantity` in-memory, set `Status = Cancelled`, `SaveChangesAsync`. **Restock-always — no Pending exemption.** Document this with a code comment pointing at `docs/adr/0001-cancellation-policy.md`.
- [ ] Returns `404 ORDER_NOT_FOUND` when the order does not exist
- [ ] Returns `401` when no JWT is present, `403` when a Customer JWT is present
- [ ] Integration tests cover the 16 cases listed in the spec under "UpdateOrderStatus integration tests", including the three restock-always cases (Pending→Cancelled, Paid→Cancelled, Shipped→Cancelled) and the "after-cancel terminal" case
- [ ] Unit tests cover the 7 `OrderStatusTransitions` cases listed in the spec
- [ ] `dotnet test` green; 15a + 15b tests still pass; no regressions in the existing customer-order suite (especially `Checkout_*` and the payment-failure tests, which share the same `Orders`/`OrderItems` tables)
