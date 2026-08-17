# Stock deduction uses an atomic SQL UPDATE, not EF tracking

Stock is decremented at checkout with a single `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty` per cart item, and the `rowsAffected` count gates the response. No row is loaded into the change tracker, no `RowVersion` column is needed, and concurrent checkouts that both see `Stock = 1` cannot both succeed — exactly one of them gets `rowsAffected = 0` and is rejected with `400 INSUFFICIENT_STOCK`.

**Why:** EF Core's change tracker performs a read-modify-write under the default Read Committed isolation, so two concurrent checkouts that read `Stock = 1` can both pass the pre-payment re-validation and then race on `SaveChangesAsync` — last write wins, the storefront oversells. The plan's own risk register flags this; the proposed mitigation ("DB transaction") does not actually fix it. Atomic conditional UPDATE is the only pattern that is correct without choosing an isolation level or adding a row version.

**Considered alternatives:**
- **`[ConcurrencyCheck]` on `Product.Stock`** — rejected because it requires catching `DbUpdateConcurrencyException`, deciding retry policy (do we re-read cart? refetch product?), and still has a small lost-update window between read and update.
- **`SELECT ... FOR UPDATE` (pessimistic)** — rejected because it holds a row lock for the entire transaction (including the payment round trip), inflating contention for what should be a sub-millisecond check.
- **Application-level re-check only** (the v1 state) — rejected because it has the race the risk register already names.

**Consequences:**
- `OrdersController.Checkout` calls `ExecuteSqlInterpolatedAsync` once per cart item, in order, before the payment call. Each call must check its own `rowsAffected`.
- If payment fails after stock has already been decremented, the controller must restock the items it deducted (a second atomic UPDATE) before returning `400 PAYMENT_FAILED`. The restock path is now mandatory, not optional.
- `Product.Stock` stays a plain `int`; no schema change.
- Tests must cover three cases: (a) happy path with concurrent checkouts — exactly one succeeds; (b) payment failure mid-checkout — stock is fully restored; (c) the re-validate-before-deduct call in the existing controller is now redundant and should be removed in Task 11a's revision.
- When variants land (ADR 0003), the atomic UPDATE targets `ProductVariants`, not `Products`. Each cart item binds to one variant.
- When reservations land (ADR 0007, v2), this ADR is superseded — reservations replace deductions entirely.

**HTTP code note:** the restock-fail path returns `400 PAYMENT_FAILED`, **not** `402`. The original `plan.md` Task 11a spec said `402`; the live code returns `400` and that matches PayPal/Adyen convention. `CONTEXT.md` (Payment → Payment failure) records the deviation. Stripe uses `402` for `PaymentIntent` failures, but the storefront's request was well-formed — the provider said no — so `400` is defensible. ADR is silent on the HTTP code; the canonical answer lives in `CONTEXT.md`.

**Status (2026-07-13):** **NOT SHIPPED.** The 2026-07-13 grilling caught that `OrdersController.Checkout` still uses the in-memory re-validate-then-deduct loop (`item.Product.Stock -= item.Quantity`). No `ExecuteSqlInterpolatedAsync` or `ExecuteSqlRaw` exists in the codebase. v1 ships with the loop; this ADR remains the target. The risk register's race-condition row remains a known open risk in v1 and is mitigated by (a) single-Postgres-instance deployment, (b) the in-memory check before `SaveChanges`, (c) the lack of a concurrent-checkout integration test in `OrdersControllerTests.cs`. Plan Task 21 / todo Task 24 should be re-opened as ⚪ Not started, not ✅ Shipped. When the rewrite lands, the test must include a concurrent-checkout case for the last unit.
