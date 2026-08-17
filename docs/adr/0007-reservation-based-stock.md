# Stock is reserved, not deducted, at checkout

A checkout creates an `OrderReservation` row per cart line, holding the requested `Quantity` against the `ProductVariant` (v2) or `Product` (v1 fallback) for a TTL (`ReservationTtlMinutes`, default 15). On payment confirmation, the reservation is converted into a stock deduction (the reserved quantity is subtracted from `ProductVariant.Stock`, the reservation row is marked `Confirmed`). On payment failure, the reservation is released and the row is deleted. On TTL expiry, a background job sweeps the table, releases unconfirmed reservations, and emits an `Abandoned` event for the order (see ADR 0006).

**Why:** v1 deducts `Product.Stock` in-memory *before* `SaveChanges`. If payment fails, the EF context is discarded and the deduction is rolled back — but the UI never sees a "your items are held for N minutes" state, two customers can both pass the in-memory check for the last unit (the race that ADR 0002 is supposed to fix but isn't), and an abandoned cart leaves a `Pending` order sitting in the DB forever. Reservations fix all three.

**Considered alternatives:**
- **Pessimistic lock on the Product row** — rejected because the lock has to outlive the payment round-trip, which serialises all checkouts against a single product. Bad for throughput, bad for the storefront.
- **In-memory reservation map (Redis, in-process dict)** — rejected because the storefront has no Redis yet and adding one is a bigger lift than the SQL table.
- **Optimistic version column on Product** — rejected because it still doesn't tell the customer "your items are held", and the lost-update window is what we're trying to close.
- **v1 in-memory loop, ADR 0002 atomic SQL UPDATE** — rejected for v2 because both still mutate `Product.Stock` synchronously. The semantic we want is "hold for N minutes, then either keep or release," and that's a reservation, not a deduction.

**Consequences:**
- New table `OrderReservations { Id, OrderId, ProductVariantId, ProductId (v1 fallback), Quantity, Status (Held/Confirmed/Released/Expired), ExpiresAt, CreatedAt }` with index on `(Status, ExpiresAt)` for the sweep.
- `ProductVariant.Stock` (or `Product.Stock` in the v1 fallback) is decremented ONLY when a reservation is `Confirmed`. The pre-payment flow does not touch it.
- The pre-payment stock check becomes "any reservation in `Held` for this product + qty does not exceed `Stock`" — i.e. the **available stock** is `Stock − SUM(Held reservations for this product)`. The `OrdersController.Checkout` flow inserts reservation rows in `Held` state and then calls the payment provider.
- On payment success: UPDATE reservations to `Confirmed`, deduct `ProductVariant.Stock`, emit a `PaymentConfirmed` event (ADR 0006).
- On payment failure: UPDATE reservations to `Released`. No stock change. Emit a `PaymentFailed` event.
- On TTL expiry (background job, runs every minute): UPDATE reservations to `Expired`, emit an `Abandoned` event for the order. The job is a hosted service in the API process or a separate worker — pick whichever the deployment allows.
- ADR 0002 (atomic SQL UPDATE) is **superseded** by reservations. The race the atomic UPDATE was supposed to fix is closed by the reservation's `Status = Held` insert, which uses a unique constraint or a transactional check to ensure `available stock ≥ reservation qty`.
- Tests must cover: (a) happy path; (b) concurrent checkouts for the last unit — exactly one wins, the other gets `400 INSUFFICIENT_STOCK`; (c) payment failure — reservations released, no stock change; (d) abandonment — TTL sweep releases reservations and emits `Abandoned`.

**Status (2026-07-13):** Not started. Phase 8 (v2). v1 ships with the in-memory loop (the open risk recorded in the plan's Risk Register).
