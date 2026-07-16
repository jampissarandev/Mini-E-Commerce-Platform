# CONTEXT.md

Ubiquitous language for this codebase. Canonical source of truth for **what things are called** (not how they are built). For *how* they're built, see `docs/adr/` and `tasks/plan.md`.

When the docs disagree with the code, the **code wins for behavior** and this file is updated to match. When a term is ambiguous, raise it in `/grilling` and resolve it here before it leaks into the API.

---

## Actors

| Term | Meaning | Avoid |
|---|---|---|
| **Customer** | An `ApplicationUser` with `role = "Customer"`. The person who places orders. | Don't call them "user" in domain text; "user" is reserved for the Identity concept. |
| **Admin** | An `ApplicationUser` with `role = "Admin"`. Manages catalog, orders, and dashboard. | Don't conflate with the system-level admin of the platform. |
| **ApplicationUser** | The ASP.NET Core Identity class. A person with a `UserName`/`Email` and a `PasswordHash`. Has a role. | Don't use "User" in domain text. |
| **Anonymous visitor** | A browser session with no JWT. Browses the catalog; cannot add to cart or check out. | Don't call them a "guest" — "guest" implies an identity, anonymous visitor implies none. |

**FK rule:** a row that points at a person uses the property name `CustomerId` (not `UserId`, not `ApplicationUserId`). The pointed-at row is an `ApplicationUser` whose role is `Customer`. The role check is enforced at the controller layer; the FK is structural. The v1 migration `20260713120000_RenameUserIdToCustomerId` completed this rename; the property is now stable.

## Catalog

| Term | Meaning | Avoid |
|---|---|---|
| **Product** | The catalog row — a *model*. Has `Name`, `Slug`, `Description`, `Price`, `Stock`, `CategoryId`, `IsActive`. Display aggregate. | Don't say "product" when you mean the thing the customer puts in the cart — that's a Variant (see ADR 0003). |
| **ProductVariant** | A *sellable unit*. Has `Sku` (unique), `Size?`, `Color?`, `Stock`, `IsActive`. Belongs to a Product. The cart and order reference Variants, not Products. **Future (ADR 0003, Phase 7 Task 27).** | Don't add `Stock` to Product after variants land — it lives on the Variant. |
| **Category** | A tree of categories via `ParentCategoryId`. Has a unique `Slug` and a `ProductCount` (counted on the fly for active products). | Don't say "department" or "section". |
| **ProductImage** | A child of a Product. Has `Url` and `SortOrder`. **Per-product** (not a global library). The same `Url` on two products = two rows. | Don't call it "asset" or "media" — those are storage-layer concerns, not domain. |
| **Slug** | The URL-safe identifier of a Product. Unique. Auto-generated from `Name` on create, editable. | Don't include spaces or non-ASCII. |

## Cart and checkout

| Term | Meaning | Avoid |
|---|---|---|
| **Cart** | A persistent collection of `CartItem`s tied to one `CustomerId`. One Cart per Customer. Created on first `GET /cart` (idempotent). | Don't call it a "basket" — that name is reserved for the future reservation cart (v2, ADR 0007). |
| **CartItem** | A line in a Cart. References a `ProductVariant` (v2) or `Product` (v1). Has `Quantity` and a `UnitPrice` **snapshot at add-time**. | Don't compute `UnitPrice` on every read — it's a snapshot. The price re-validation happens at checkout, not on cart display. |
| **Checkout** | Converting a Cart into an Order. Validates stock, charges payment, deducts stock (v1) or reserves stock (v2), creates Order, clears Cart. | Don't conflate with "Place order" button click — Checkout is the whole flow. |

## Orders and fulfillment

| Term | Meaning | Avoid |
|---|---|---|
| **Order** | A completed checkout. One row per checkout attempt. Has `Status`, `Subtotal`, `ShippingFee`, `Total`, a shipping-address snapshot (flat `Shipping*` fields), and `Items` (each a snapshot of product name + price). v1 mutates the row through `Status` transitions. **v2 (ADR 0006, Phase 8) event-sources it.** | Don't call it a "purchase" or a "transaction" (transaction is overloaded with DB transactions). |
| **OrderItem** | A line in an Order. Snapshots `ProductName` (or "ProductName (Size, Color)" in v2) and `UnitPrice` at order time. | Don't look up the live product on Order display — the snapshot is the truth. |
| **OrderStatus** | The lifecycle state. v1 enum: `Pending, Paid, Shipped, Delivered, Cancelled`. **v1 semantics: `Pending` is a persisted state visible to the customer; `Paid` flips on `SaveChanges`.** **v2 (ADR 0006 + 0007): `Pending` is a transient state during reservation; only `Paid` is persisted; transitions are appended to an `OrderEvent` stream.** | Don't reuse `OrderStatus` values across contexts (e.g. "pending payment" vs "pending fulfilment" — both called `Pending` in v1, which is why v2 splits them). |
| **Pending** (v1) | Order created, stock deducted in-memory, payment attempted. Visible to the customer. Flips to `Paid` on `SaveChanges` or stays `Pending` if payment fails (caller gets `400 PAYMENT_FAILED`). **v1 has no auto-expiry** — abandoned Pending orders sit in the DB until manually cleaned up. | — |
| **Cancellation** | Admin-driven transition to `Cancelled`. v1 always restocks items (the "no-op for Pending" rule is dropped — the live controller deducts stock in-memory before `SaveChanges`, so every Pending has stock to restock). v2 reserves-not-deducts, so cancellation always releases the reservation. | Don't use "cancellation" for customer abandonment of a Pending order. |
| **Abandonment** | A Pending order whose customer closed the browser / never paid. v1: row sits in the DB; manual cleanup. v2 (ADR 0007): auto-expire after N minutes; a background job releases the reservation. | Don't conflate with Cancellation. Abandonment is not a transition; cancellation is. |

## Shipping

| Term | Meaning | Avoid |
|---|---|---|
| **Address** | A Customer's saved shipping address. v2 only (ADR 0004, Phase 7 Task 26). Has `FullName, Street, City, PostalCode, Country, Phone, IsDefault`. A Customer has 0..n Addresses; exactly one may have `IsDefault = true`. | Don't call it "shipping address" or "delivery address" — an Address is reused across orders. |
| **Shipping snapshot** | The flat `Shipping*` fields on the Order row (`ShippingFullName, ShippingStreet, …`). Captures the address at the time of order. Editing or deleting the source Address later does NOT mutate historical Orders. **v1: typed at checkout; v2: copied from a chosen Address or typed as a one-off.** | Don't put a FK from Order to Address — the snapshot is the source of truth. |

## Money

| Term | Meaning | Avoid |
|---|---|---|
| **Money** | `decimal` in C# (not `double`/`float`). Display in `USD` only. Tax, multi-currency, and discounts are **explicitly out of scope** for v1. | Don't introduce a `Money` value object yet — premature for the scope. |
| **Subtotal** | Sum of `CartItem.UnitPrice * CartItem.Quantity` at checkout time. | Don't include shipping in Subtotal. |
| **ShippingFee** | A single config-driven constant (`5.99` USD in v1) read from `appsettings.json` (`Shipping:Fee`) via `IOptions<ShippingOptions>`. Per-region rates, free-shipping thresholds: out of scope. | Don't hardcode in the controller. |
| **Total** | `Subtotal + ShippingFee`. | Don't subtract discounts (out of scope). |

## Auth

| Term | Meaning | Avoid |
|---|---|---|
| **Access token** | Short-lived JWT (60-min default). Carries `sub` (user id), `email`, `role`, `fullName` claims. Sent as `Authorization: Bearer <token>`. | Don't put the access token in a cookie or localStorage. |
| **Refresh token** | Long-lived (30-day default), single-use-per-rotation, server-side state. Delivered as an `httpOnly`, `Secure`, `SameSite=Lax` cookie scoped to `/api/auth`. **Future (ADR 0005, Phase 7 Task 25).** v1: re-login on access token expiry. | Don't put the refresh token in localStorage — XSS. |
| **Role claim** | The `ClaimTypes.Role` value in the JWT. `[Authorize(Roles = "Admin")]` is gated on it. **Token validation parameters must set `RoleClaimType = ClaimTypes.Role`** to match the emitted claim — the `JwtSecurityTokenHandler` remaps short names to URIs by default. | Don't emit `"role"` (short name) and assume `[Authorize(Roles = "Admin")]` works. |

## Image storage

| Term | Meaning | Avoid |
|---|---|---|
| **Image storage** | The strategy behind `IImageStorage`. v1: `LocalImageStorage` writes to `wwwroot/images/{yyyy}/{mm}/{guid}.{ext}`. Future: Cloudinary, S3. | Don't hardcode `wwwroot/images/...` in business code — go through `IImageStorage.GetPublicUrl`. |
| **Image validation** | Done in `LocalImageStorage.SaveAsync` via ImageSharp: detect format + reject > 5 MB. | Don't re-validate at the controller layer. |

## Payment

| Term | Meaning | Avoid |
|---|---|---|
| **Payment provider** | The strategy behind `IPaymentService`. v1: `MockPaymentService`. Future: Stripe, PayPal. | Don't call it "Stripe" or "PayPal" generically. |
| **Mock payment mode** | v1: `AlwaysSucceed` (default) / `AlwaysFail` / `FailIfAmountGreaterThan(threshold)`. Bound from `appsettings.json` (`Payments:Mock:Mode`). Exposed to the frontend via `GET /api/payments/mock-mode` (no auth, non-sensitive). Dev-only amber banner in `Checkout` when mode ≠ `AlwaysSucceed`. | Don't put the threshold in the controller. |
| **Payment failure** | v1: returns `400 PAYMENT_FAILED` (**NOT** `402` — `plan.md` originally said `402`, but the live code uses `400`, matching PayPal/Adyen convention). On failure, no `SaveChanges` is called, so stock deduction is rolled back (EF discards tracked changes). | Don't return `402` for client-side payment rejection — the request was well-formed; the provider said no. |
| **Refund** | Out of scope for v1. v2: when a real payment provider lands, cancellation of a Paid order triggers a refund via the provider, not via the mock. | Don't try to reverse a charge through the mock. |

---

## Cross-cutting rules

1. **Idempotent reads.** `GET /cart` creates the cart on first call. `GET /categories` always returns the full list. No hidden side effects on GET.
2. **Soft delete by default.** `DELETE /admin/products/:id` sets `IsActive = false` (the product disappears from the public catalog but stays in the DB for order history). `?hard=true` is the only way to actually remove a row, and it's blocked with `409 PRODUCT_IN_USE` if any order or cart references it.
3. **Snapshot pattern.** Anything captured at order time (shipping address, product name, unit price) is copied onto the Order/OrderItem. The source can mutate or be deleted; the historical record is stable.
4. **Role gating on the controller, not the service.** `[Authorize(Roles = "Admin")]` lives on the controller action. The service assumes the caller has been authorized; it doesn't re-check.
5. **Response envelope.** Every controller returns `ApiResponse<T>` with `Success`, `Data`, `Error`, `Meta`. Error is an `ApiError` with `Code` (machine-readable) and `Message` (human-readable). The frontend parses `Code`, never `Message`.
6. **Exceptions → middleware.** `ExceptionMiddleware` maps `NotFoundException → 404`, `ValidationException → 400`, `BusinessRuleException → 409`, `UnauthorizedAccessException → 401`, else `500` (no stack trace leaked).
7. **Money is `decimal`.** Never `double` or `float`.
8. **Timestamps are UTC.** `DateTime.UtcNow` on write. Display layer formats to local time.
9. **v1 stock is in-memory, v2 stock is reserved.** v1 deducts `Product.Stock` in-memory before `SaveChanges`; on payment failure the EF context is discarded. v2 (ADR 0007) uses a reservation table so cancellation and abandonment have explicit semantics.
10. **"Snapshot" is the truth, not the source.** `OrderItem.ProductName`, `OrderItem.UnitPrice`, and the `Order.Shipping*` fields are historical fact at the time of order. Editing the source Product/Address does not retroactively change them.

---

## Out of scope (v1)

- Tax, multi-currency, discounts.
- Returns / refunds (real provider required).
- Customer self-cancel.
- Email / notifications.
- Image CDN signing for production.
- Product variants (ADR 0003 → Phase 7, Task 27).
- Customer address book (ADR 0004 → Phase 7, Task 26; the snapshot half is shipped).
- Silent token refresh (ADR 0005 → Phase 7, Task 25).
- Event-sourced orders (ADR 0006 → Phase 8, v2).
- Reservation-based stock (ADR 0007 → Phase 8, v2).
- Atomic SQL UPDATE for stock deduction (ADR 0002 — NOT shipped in v1; see ADR 0002 Status).
