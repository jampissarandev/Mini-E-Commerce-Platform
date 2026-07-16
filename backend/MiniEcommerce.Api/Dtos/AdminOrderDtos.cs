namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Request DTOs ═══════════════════

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
/// all items with computed subtotals, shipping address, and totals.
/// Used by both <c>GET /api/admin/orders/:id</c> (15b) and
/// <c>PUT /api/admin/orders/:id/status</c> (15c, echoes updated order).
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
    public decimal Subtotal => UnitPrice * Quantity;
}
