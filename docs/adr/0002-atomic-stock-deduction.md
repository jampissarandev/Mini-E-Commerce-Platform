# Stock deduction uses an atomic SQL UPDATE, not EF tracking

Stock is decremented at checkout with a single `UPDATE Products SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty` per cart item, and the `rowsAffected` count gates the response. No row is loaded into the change tracker, no `RowVersion` column is needed, and concurrent checkouts that both see `Stock = 1` cannot both succeed — exactly one of them gets `rowsAffected = 0` and is rejected with `400 INSUFFICIENT_STOCK`.

**Why:** EF Core's change tracker performs a read-modify-write under the default Read Committed isolation, so two concurrent checkouts that read `Stock = 1` can both pass the pre-payment re-validation and then race on `SaveChangesAsync` — last write wins, the storefront oversells. The plan's own risk register flags this; the proposed mitigation ("DB transaction") does not actually fix it. Atomic conditional UPDATE is the only pattern that is correct without choosing an isolation level or adding a row version.

**Considered alternatives:**
- **`[ConcurrencyCheck]` on `Product.Stock`** — rejected because it requires catching `DbUpdateConcurrencyException`, deciding retry policy (do we re-read cart? refetch product?), and still has a small lost-update window between read and update.
- **`SELECT ... FOR UPDATE` (pessimistic)** — rejected because it holds a row lock for the entire transaction (including the payment round trip), inflating contention for what should be a sub-millisecond check.
- **Application-level re-check only** (the current state) — rejected because it has the race the risk register already names.

**Consequences:**
- `OrdersController.Checkout` calls `ExecuteSqlInterpolatedAsync` once per cart item, in order, before the payment call. Each call must check its own `rowsAffected`.
- If payment fails after stock has already been decremented, the controller must restock the items it deducted (a second atomic UPDATE) before returning `402 PAYMENT_FAILED`. The restock path is now mandatory, not optional.
- `Product.Stock` stays a plain `int`; no schema change.
- Tests must cover three cases: (a) happy path with concurrent checkouts — exactly one succeeds; (b) payment failure mid-checkout — stock is fully restored; (c) the re-validate-before-deduct call in the existing controller is now redundant and should be removed in Task 11a's revision.
