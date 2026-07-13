# Customer owns an Address Book; Orders snapshot an Address

A Customer has 0..n `Address` rows. Each Address has `FullName, Street, City, PostalCode, Country, Phone`, plus `IsDefault` (a Customer may mark one Address as the default shipping address; only one is default at a time). The Checkout flow lets the Customer pick an existing Address or create a new one. The Order stores a snapshot of the chosen Address inline on the Order (`ShippingFullName`, `ShippingStreet`, …) so an Address later edited or deleted does not retroactively change historical orders.

**Why:** "type your address every time" is the first thing real customers complain about. Even a small storefront with repeat buyers benefits from a saved address. Snapshotting the address onto the Order keeps the historical record stable (matches the snapshot pattern we already use for `OrderItem.ProductName` and `UnitPrice`) and lets us delete addresses without orphaning order history.

**Considered alternatives:**
- **Flat shipping fields on Order only** (the current state) — rejected for v1 because it forces every repeat customer to retype the address and provides no default-shipping UX.
- **Address per Order only, with a `defaultAddressId` on Customer** — rejected because it conflates the address record with the order, so editing an address mutates all past orders that referenced it.
- **Snapshot on Order + nullable FK to Address (no address book)** — rejected because it leaves the customer with no UI to manage addresses and no way to set a default.

**Consequences:**
- New table `Addresses { Id, CustomerId, FullName, Street, City, PostalCode, Country, Phone, IsDefault, CreatedAt }` in a Task 3a revision migration.
- `Order` keeps its `Shipping*` flat fields for the snapshot. No FK from `Order` to `Address` — the snapshot is the source of truth for historical orders.
- New endpoints: `GET/POST/PUT/DELETE /addresses`, `PUT /addresses/:id/default`. The Customer's first checkout can pre-fill from the default Address or accept a one-off address without saving.
- Checkout form gains a "Save this address" checkbox and a "Use a saved address" picker.
- Setting `IsDefault = true` on one address must unset it on the others in the same transaction (single default enforced in the service layer, not in the DB constraint, to keep the migration simple).
- Tax, discount, multi-currency remain explicitly out of scope for v1; when any of those lands, it does *not* require a schema change to `Address`.
