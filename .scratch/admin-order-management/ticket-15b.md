# 15b — Admin order detail endpoint

**What to build:** `GET /api/admin/orders/:id` returns the full detail of any single order — including the customer identity (email + full name), every `OrderItem` with its snapshotted product name and unit price, the shipping address, the totals, and the status. Admins can read any order; customers cannot read orders they don't own via this endpoint (and can't read this endpoint at all because the controller is admin-gated).

**Parent:** #5

**Blocked by:** #15a (the DTO file is introduced in 15a; this ticket extends it and reuses its conventions)

**Status:** ready-for-agent

- [ ] `GET /api/admin/orders/{id:int}` on the existing `AdminOrdersController`
- [ ] Returns `200` with `ApiResponse<AdminOrderDetail>` for any existing order, regardless of customer
- [ ] `AdminOrderDetail` fields: every `Order` column (id, status, subtotal, shippingFee, total, all `Shipping*` fields, createdAt) **plus** `customer { id, email, fullName }` **plus** `items: [ { id, productId, productName, unitPrice, quantity, subtotal } ]` where `subtotal` is `unitPrice * quantity` computed server-side
- [ ] Returns `404 ORDER_NOT_FOUND` when no order matches the id
- [ ] Returns `401` when no JWT is present, `403` when a Customer JWT is present (customers must not be able to read other customers' orders via the admin route, even by guessing an id)
- [ ] Reuses the `AdminOrderDtos` file added in 15a; does not introduce a parallel DTO file
- [ ] Integration tests cover the 5 cases listed in the spec under "GetOrderById integration tests" (role gating including the cross-tenant customer case, full detail shape including computed item subtotals, 404)
- [ ] `dotnet test` green; 15a tests still pass
