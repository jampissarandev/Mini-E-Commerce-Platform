# Mini E-Commerce Platform

The single bounded context for this app: a customer-facing storefront with admin management. Customers browse a product catalog, build a cart, check out, and track orders; admins manage products and orders.

## Language

**Customer**:
A person who has registered an account, can place orders, and is the actor for cart, checkout, and order history.
_Avoid_: User (use User only when referring to the ASP.NET Core Identity technical concept — `ApplicationUser`, `UserManager<ApplicationUser>`, `AspNetUsers` table)

**Cart**:
A customer's persistent collection of products they intend to purchase. One Cart per Customer (uniqueness enforced on `Cart.CustomerId`). Items in the Cart are not reserved — stock is only deducted at checkout. A Cart becomes an Order at checkout; the Cart is then cleared.
_Avoid_: Basket, Shopping cart, Wishlist (Wishlist is a different concept: saved-for-later without purchase intent)

**Order**:
A completed checkout. Created from a Cart at the moment the customer submits checkout. Once an Order exists, it is the canonical record of what the customer bought, at what price, and where it ships.
_Avoid_: Transaction, Purchase (use Purchase only for the external payment record from the payment provider, never for the customer's order)

**OrderItem**:
A line in an Order. Snapshots `ProductName` and `UnitPrice` at the time of order creation, so a later Product rename or reprice does not retroactively change historical orders.
_Avoid_: Line item, Order line

**OrderStatus**:
The lifecycle state of an Order. States: `Pending` → `Paid` → `Shipped` → `Delivered`. `Cancelled` is a terminal off-ramp from `Pending` or `Paid`. `Delivered` and `Cancelled` are terminal. Transitions are guarded; see `docs/adr/0001-cancellation-policy.md` for who can drive transitions and restock rules.

**Category**:
A taxonomy node a Product belongs to. Categories form a tree via `ParentCategoryId` (nullable root). The catalog UI may render the tree (expand/collapse) or flatten it for filter chips; the API exposes both shapes. A Product belongs to exactly one Category.
_Avoid_: Department (use only for a top-level non-tree grouping, if added later), Collection, Tag (Tag is many-to-many free-form; out of scope)

**Product**:
A *model* in the catalog (e.g. "Men's Crew-Neck T-Shirt"). It is not directly sellable; customers add a ProductVariant to the cart. Carries aggregate display fields (`Name`, `Description`, `BasePrice`, primary image). See `docs/adr/0003-product-variants.md` for why a Product has 1..n Variants.
_Avoid_: SKU (a SKU is on a Variant, not a Product), Item

**ProductVariant**:
A *sellable unit* under a Product. Carries `Sku` (unique), optional `Size`, `Color` and other future attributes, and its own `Stock`. `CartItem` and `OrderItem` reference a Variant, never a Product directly.
_Avoid_: SKU (a Variant *has* a SKU; do not use "SKU" to mean the Variant itself), Option (an Option is a free-form key/value on a cart line, not the entity)

**Address**:
A shipping address belonging to a Customer (an "address book" entry). One Customer has 0..n Addresses; one may be marked `IsDefault`. At checkout, the Customer picks an existing Address or types a one-off one. The chosen Address is snapshotted onto the Order as flat `Shipping*` fields, so editing or deleting the Address later does not change past Orders. See `docs/adr/0004-customer-address-book.md`.
_Avoid_: ShippingAddress (use ShippingAddress only for the snapshot on Order, never for the Customer's reusable record)

**Money** (v1 scope): Orders hold `Subtotal` + `ShippingFee` + `Total`. `ShippingFee` is a single config-driven constant read from `appsettings.json` (`Shipping:Fee`). Currency is hardcoded USD. Tax, discounts, coupons, and multi-currency are explicitly out of scope for v1.

**Payment** (v1 scope): the storefront uses `IPaymentService` with `MockPaymentService` as the v1 implementation. The mock supports three modes — `AlwaysSucceed` (default), `AlwaysFail`, and `FailIfAmountGreaterThan(threshold)` — so the failure path in `OrdersController.Checkout` is testable end-to-end. Real providers (Stripe, PayPal) are out of scope; the `IPaymentService` interface is the swap point.

**Authentication** (v1 scope): short-lived JWT access token (60 min, returned in JSON) + long-lived refresh token (30 days, `httpOnly` cookie, silent rotation). The frontend retries a 401 once via the refresh endpoint before logging out. See `docs/adr/0005-token-refresh.md`.
