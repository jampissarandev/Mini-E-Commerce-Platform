# Order cancellation is admin-only; restock always; abandonment is not cancellation

Cancellation can only be driven by an Admin role — Customers cannot cancel their own Orders. On cancellation, items are **always** restocked to `Product.Stock` (the historical "no-op for Pending" rule is dropped — see Status below). Abandonment of a Pending order is **not** cancellation; it is a separate lifecycle state with no transition (see v2 ADR 0007 for the auto-expiry that replaces it).

**Why:** the storefront currently runs on a mock payment provider with synchronous capture, so any `Paid` order has already charged the customer. Letting customers self-cancel would require automatic refund logic the mock does not provide, and adding Stripe/PayPal refund flows is out of scope for the learning track. Centralising the cancel decision in the Admin role keeps the policy explicit and easy to evolve when a real payment provider lands.

**Considered alternatives:**
- **Customer self-cancel pre-ship** (Amazon-style) — rejected because it requires refund automation that is out of scope.
- **No auto-restock on cancel** (B2B) — rejected because the storefront sells to consumers who expect the unit to be available to others once an order is voided.
- **No-op restock for Pending orders** (the original v1 rule) — rejected in the 2026-07-13 grilling. The live `OrdersController.Checkout` deducts stock in-memory *before* `SaveChanges`, so every Pending order has stock to restock. The "no-op" exception was a holdover from the in-memory re-validate-then-deduct loop and is now removed.

**Consequences:**
- `PUT /admin/orders/:id/status` (Task 15c) must enforce role gating and transition rules; an attempt to skip from `Shipped`/`Delivered` returns `409 INVALID_TRANSITION`.
- Restock runs on every cancel, regardless of `Status`. There is no Pending exemption.
- **Abandonment ≠ cancellation.** A Pending order whose customer closed the browser is a row that sits in the DB; it is not a transition. v1 has no auto-expiry. v2 (ADR 0007) adds a background job that releases the reservation after N minutes and marks the order `Abandoned` (a v2-only status that does not appear in the v1 enum).
- When a real payment provider is added (Stripe), revisit: refund flow + restock become two distinct steps, and the no-op semantics for `Pending` go away.

**Status (2026-07-13):** Live. `OrdersController.Checkout` deducts stock in-memory and `SaveChanges` is the gate. Admin cancel restock is shipped in Task 15c. The abandonment/auto-expire path is **NOT** shipped in v1 and is deferred to Phase 8 (ADR 0007).
