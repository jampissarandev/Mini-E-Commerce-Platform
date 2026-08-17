using System.ComponentModel.DataAnnotations;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Request DTOs ═══════════════════

/// <summary>
/// Request body for <c>PUT /api/admin/orders/{id}/status</c>.
///
/// Validation runs through the framework's DataAnnotations pipeline. The
/// project's <c>InvalidModelStateResponseFactory</c> in <c>Program.cs</c>
/// maps model-state failures to an <c>ApiResponse</c> with error code
/// <c>VALIDATION_ERROR</c> — same envelope the rest of the API uses.
/// </summary>
public record UpdateOrderStatusRequest : IValidatableObject
{
    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [Required] handles the missing/empty case; only validate the
        // enum-membership here.
        if (string.IsNullOrWhiteSpace(Status)) yield break;

        if (!Enum.TryParse<OrderStatus>(Status, ignoreCase: false, out _))
        {
            yield return new ValidationResult(
                $"Status '{Status}' is not a valid order status. Valid values: {string.Join(", ", Enum.GetNames<OrderStatus>())}.",
                new[] { nameof(Status) });
        }
    }
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
