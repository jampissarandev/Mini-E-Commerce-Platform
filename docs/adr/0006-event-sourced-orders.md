# Orders are event-sourced; the Order row is a projection

An Order is the **projection** of an append-only `OrderEvent` stream. Each state change (`Created`, `StockReserved`, `Paid`, `Shipped`, `Delivered`, `Cancelled`, `Refunded`, `Abandoned`) is one row in `OrderEvents` with `OrderId, EventType, PayloadJson, OccurredAt, ActorId?`. The current `Order` row is rebuilt by replaying events for the `OrderId`, and the read-side `OrderItems` table is the materialised view that the catalog/UI queries.

**Why:** v1 mutates the `Order` row in place through `Status` transitions. That works for a learning project, but it has two real problems:

1. **Audit trail is missing.** When an admin flips `Paid → Cancelled`, there is no record of who did it, when, or what state the order was in at the time. For a real storefront, that is a compliance and customer-support hole.
2. **`Pending` is overloaded.** v1 uses `Pending` for "stock deducted in memory, payment in flight" and the system has no clean way to say "we created an order, the customer never came back." `Abandonment` is bolted on as a comment, not a state.

Event-sourcing solves both. Every state change is recorded. The Order is whatever the latest event says it is. `Abandonment` becomes a first-class event, not a missing transition.

**Considered alternatives:**
- **Keep v1 (mutate-in-place) forever** — rejected because the audit hole is real and the `Pending` overload makes the controller logic fragile. v2 is the time to fix it.
- **Outbox + CDC** — rejected because the storefront has no need for distributed transactions or downstream consumers. The events live in the same Postgres.
- **Pure event store, no projection** — rejected because the catalog UI needs to query "show me the user's orders" without replaying thousands of events per request. The projection table is the read path; the event log is the source of truth.

**Consequences:**
- New table `OrderEvents { Id, OrderId, EventType, PayloadJson, OccurredAt, ActorId? }` with index on `(OrderId, OccurredAt)`.
- `Order.Status` becomes a computed field (`MAX(OccurredAt) → EventType` mapped to the v1 enum), or a denormalised cache updated by a `OrderEvent` trigger / projection rebuild. v2 chooses: keep `Status` as a denormalised column updated transactionally with the event insert.
- `OrderItems` stays as a projection (one row per cart line at order time). On a `Cancelled` event, items are NOT deleted — the projection reflects "the order was cancelled" and the `OrderItems` rows stay as historical fact.
- The atomic-stock-deduction pattern (ADR 0002) is **superseded** by reservations (ADR 0007) — events are the only place stock moves.
- Concurrency: a single `OrderId` can be the target of multiple events from different actors (customer + admin + background job). The event insert is the linearisation point; the read-side `Status` column is updated in the same transaction.
- The `Created` event carries the full snapshot (items, shipping, total) — the projection is reconstructable from one event. Subsequent events only carry what changed.
- Tests must cover: (a) replay produces identical `Order` row; (b) two events on the same order are linearised by `OccurredAt`; (c) the `Status` denormalised column stays consistent with the latest event.

**Relationship to ADR 0007:** the event log and the reservation table are written together. A `StockReserved` event is the "I am holding this stock" record; a `PaymentConfirmed` event is the "I am keeping this stock" record; a `PaymentFailed` event releases the reservation. The reservation table can be reconstructed from the event log; we keep it as a queryable projection for the dashboard and the abandonment sweep.

**Status (2026-07-13):** Not started. Phase 8 (v2). v1 ships with mutate-in-place. This ADR is the canonical reason `v1 Order` is a "phase 1, throwaway" design — not a v1 limitation we plan to keep.
