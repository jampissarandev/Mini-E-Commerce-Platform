namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Request DTOs ═══════════════════

/// <summary>
/// Request body for <c>PUT /api/admin/orders/{id}/status</c>.
/// Validation is performed in the controller action rather than via
/// <c>[Required]</c> or <c>IValidatableObject</c> so that validation
/// failures are consistently returned as <c>ApiResponse</c> with error
/// code <c>VALIDATION_ERROR</c> through <c>ExceptionMiddleware</c>.
/// </summary>
public record UpdateOrderStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

// ═══════════════════ Response DTOs ═══════════════════

public record AdminOrderListItem
{
    public int Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public int ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Full detail of an order for the admin view. Includes customer identity,
/// all items with snapshotted subtotals, shipping address, and totals.
/// </summary>
public record AdminOrderDetail
{
    public int Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal Total { get; init; }
    public string ShippingFullName { get; init; } = string.Empty;
    public string ShippingStreet { get; init; } = string.Empty;
    public string ShippingCity { get; init; } = string.Empty;
    public string ShippingPostalCode { get; init; } = string.Empty;
    public string ShippingCountry { get; init; } = string.Empty;
    public string ShippingPhone { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public AdminOrderCustomer Customer { get; init; } = null!;
    public List<AdminOrderItemDto> Items { get; init; } = [];
}

public record AdminOrderCustomer
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}

public record AdminOrderItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }

    /// <summary>
    /// Snapshotted line subtotal (<c>UnitPrice * Quantity</c>) computed at
    /// server-side mapping time. Kept as an <c>init</c>-only property so it
    /// survives deserialisation and re-serialisation as a fixed historical
    /// value, per the snapshot contract in <c>CONTEXT.md</c> rule #10.
    /// </summary>
    public decimal Subtotal { get; init; }
}
