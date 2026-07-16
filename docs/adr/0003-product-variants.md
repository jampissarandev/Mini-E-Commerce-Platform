# Product variants and SKUs are first-class

A `Product` is a *model* (e.g. "Men's Crew-Neck T-Shirt"); a `ProductVariant` is a *sellable unit* (e.g. "Black, Medium, SKU `TS-BLK-M`"). A Product has 1..n Variants. The `Cart` and `Order` reference Variants, not Products. `ProductVariant` carries its own `Sku`, optional `Size` and `Color` (or future attributes), and its own `Stock`. The `Product` row keeps aggregate fields (`Name`, `Description`, `Price` for catalog display) but is no longer the thing you add to a cart.

**Why:** the current model — one Product = one sellable unit — collapses the moment a customer wants to buy a shirt in three colours and two sizes. Today that means six near-duplicate `Product` rows, no link between them, six stock counts to update, and the catalog UI cannot show "T-Shirt → 6 options" without hand-rolling the grouping. Variants make the sellable unit explicit, give each its own SKU for fulfilment, and let the catalog UI render the parent-with-options pattern customers expect.

**Considered alternatives:**
- **JSON `Options` column on `Product`** — rejected because it puts shape decisions in the database, makes stock-by-option impossible without `jsonb` indexes, and ties every consumer to string parsing.
- **Cart-level options, no entity** — rejected because stock checks at checkout still need to know which variant the customer wanted.
- **No variants, six Products** (the v1 state) — rejected because the "shopping for a t-shirt" experience breaks the moment a customer sees the second colour.

**Consequences:**
- Add `ProductVariant { Id, ProductId, Sku (unique), Size?, Color?, Stock, IsActive }` in a Task 3b revision migration. Rename the old `Product.Stock` to `ProductVariant.Stock` (drop the column on `Products`).
- `CartItem` and `OrderItem` switch their FK from `ProductId` to `ProductVariantId`. Their `UnitPrice` snapshot stays.
- `POST /cart/items` and `GET /products/:id` change shape to expose variants; the frontend `ProductCard` either lists variants inline or lands on a detail page that does.
- `OrderItem.ProductName` becomes "the parent's `Name` + chosen `Size`/`Color`", e.g. "Men's Crew-Neck T-Shirt (Black, M)". Decide the formatting once and pin it in a small ADR or a domain rule.
- The atomic-stock-deduction pattern (ADR 0002) applies per variant, not per product — `UPDATE ProductVariants SET Stock = Stock - @qty WHERE Id = @id AND Stock >= @qty`.
- Category tree (see `CONTEXT.md`) is independent of this ADR; both can ship.

**Migration strategy (v1 → variants):** When Task 27a lands, the migration runs in three steps inside one transaction:

1. **Add `ProductVariants` table** (Id, ProductId FK, Sku unique, Size?, Color?, Stock int, IsActive bool, CreatedAt). Nullable at first.
2. **Backfill one default variant per existing Product** with `Sku = "LEGACY-{ProductId}"`, `Stock = current Product.Stock`, `IsActive = true`. Every existing `ProductImage` is reattached to the new variant (or stays on the Product if the image is display-level).
3. **Backfill `CartItem.ProductVariantId` and `OrderItem.ProductVariantId`** from the existing `ProductId` → matches the LEGACY variant. The columns are flipped to non-null. The old `ProductId` columns on `CartItem` and `OrderItem` are dropped.

Existing customers with open carts automatically re-bind to the LEGACY variant on next read; no "clear your cart" UX is required. The SKU format `LEGACY-{n}` is a smell (fulfilment will see SKUs like `LEGACY-7`) and is acceptable as a one-time cost — Phase 7 task 27a can re-SKU manually or add a one-off admin endpoint to bulk-rename.

**Status (2026-07-13):** Not started. Phase 7 Task 27. ADR 0002's per-variant UPDATE is contingent on this ADR landing.
