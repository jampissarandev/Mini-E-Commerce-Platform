# API snapshot carve-outs for variant attributes and navigation keys

`OrderItemDto` (customer) and `AdminOrderItemDto` (admin) expose `Size` and `Color` as separate fields alongside the snapshot `ProductName` string, and `AdminOrderItemDto` additionally exposes `Sku`. `CartItemDto` exposes `ProductId` alongside `ProductVariantId` for stable deep-link navigation. These are deliberate carve-outs from the "snapshot is the only thing the wire sees" rule (`CONTEXT.md` cross-cutting rule #10) and from the "CartItem references a Variant, not a Product" rule (`CONTEXT.md` → Cart and checkout).

**Why:** the snapshot rule is correct as a *historical record* — `ProductName` and `UnitPrice` capture the customer's purchase at order time and never drift — but two real use cases need *programmatic* access that the string cannot serve:

1. **Variant attribute rendering on past orders.** `OrderHistory` and `OrderConfirmation` want to show the chosen size and color separately so a customer can confirm "yes, that's the M I ordered" without parsing the string. The frontend already has `<VariantAttributes>` (extracted 2026-08-22 in `db9d298`) that takes `size` and `color` as props; reusing it on the order pages needs the fields on the wire.
2. **Admin fulfilment needs the SKU.** Picking from a warehouse requires the SKU on the printed pick list. The customer-side `OrderItemDto` deliberately does **not** expose `Sku` — it's an internal inventory identifier with no customer value and adds API surface for no benefit. `AdminOrderItemDto` does expose it because the warehouse picker needs it.

The `CartItemDto.ProductId` carve-out has a different motivation: `Product.Slug` is editable (`CONTEXT.md` → Catalog), so any cart row that stores the slug as a navigation key breaks when the admin renames the product. Carrying the stable `ProductId` integer instead — even though the cart's *referenced entity* for stock and checkout logic is the Variant — gives the cart sheet a permanent deep link to `/products/:id` that survives slug renames.

**Considered alternatives:**

- **Don't expose variant attributes at all.** Rejected. Forces string parsing on the frontend (fragile to formatter changes) and forbids admin re-order, "filter past orders by size", and any programmatic reuse of the snapshot.
- **Expose `Sku` on the customer `OrderItemDto` too.** Rejected. `Sku` is internal inventory semantics. The customer never needs to see or type it; exposing it adds an API surface field with no customer-facing benefit and a small privacy/inventory-leak concern.
- **Drop `CartItemDto.ProductId`, navigate via `ProductSlug` only.** Rejected. `Product.Slug` is editable, so the navigation key would break on rename. The `ProductId` integer is stable for the lifetime of the row.
- **Single DTO shared by customer and admin.** Rejected. The asymmetry is the point: admin gets `Sku`, customer doesn't.

**Consequences:**

- `OrderItemDto` gains `Size` and `Color` (nullable strings). They are populated at checkout from the chosen `ProductVariant.Size` and `Color` and are part of the snapshot — like `ProductName`, they do not change if the source variant is later edited. (Pin via test: snapshot stability.)
- `AdminOrderItemDto` gains `Size`, `Color`, and `Sku`. Same snapshot semantics. `Sku` is a copy of `ProductVariant.Sku` at order time and survives variant edits.
- `CartItemDto.ProductId` is documented as a navigation-key carve-out in `CONTEXT.md` (with a cross-reference to this ADR). It is not a re-introduction of "the cart references a Product" — the FK for stock/checkout is still `ProductVariantId`.
- These three additions are **additive** — existing fields are unchanged. The frontend may migrate off `ProductName` for variant display but does not have to. `<VariantAttributes>` already accepts `size`/`color` props.
- Future order-history features (admin re-order, "filter past orders by size") can rely on the split fields without re-parsing the snapshot string.
- Future SKU changes to `ProductVariant.Sku` do not retroactively change historical orders' snapshotted `Sku` — the snapshot rule still holds, just one level deeper.

**Status (2026-08-23):** Accepted. Resolve the round-4 / Task-27-review "Decide later" followups (Decide-later items #2, #3, #4 in `tasks/todo.md`). Implementation pending under `/mattpocock-skills implement` with the TDD skill — backend DTO + mapper changes first (Phase B), then frontend wire-up + `ProductCard` quick-add (Phase C).