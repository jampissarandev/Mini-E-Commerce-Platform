# Order cancellation is admin-only; restock always

Cancellation can only be driven by an Admin role — Customers cannot cancel their own Orders. On cancellation, items are always restocked to `Product.Stock` (idempotent: a no-op if the Order was still `Pending` and stock had not yet been deducted).

**Why:** the storefront currently runs on a mock payment provider with synchronous capture, so any `Paid` order has already charged the customer. Letting customers self-cancel would require automatic refund logic the mock does not provide, and adding Stripe/PayPal refund flows is out of scope for the learning track. Centralising the cancel decision in the Admin role keeps the policy explicit and easy to evolve when a real payment provider lands.

**Considered alternatives:**
- **Customer self-cancel pre-ship** (Amazon-style) — rejected because it requires refund automation that is out of scope.
- **No auto-restock on cancel** (B2B) — rejected because the storefront sells to consumers who expect the unit to be available to others once an order is voided.

**Consequences:**
- `PUT /admin/orders/:id/status` (Task 15c) must enforce role gating and transition rules; an attempt to skip from `Shipped`/`Delivered` returns `409 INVALID_TRANSITION`.
- Restock runs on every cancel; for `Pending` cancels it is a no-op. Test must cover both paths.
- When a real payment provider is added (Stripe), revisit: refund flow + restock become two distinct steps, and the no-op semantics for `Pending` go away.
