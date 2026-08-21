using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
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
    [SwaggerSchema("Target order status. Must be a valid transition from the current status.")]
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

[SwaggerSchema("Compact order row for the admin orders table.")]
public record AdminOrderListItem
{
    [SwaggerSchema("Order id.")]
    public int Id { get; init; }

    [SwaggerSchema("Customer's ApplicationUser id.")]
    public string CustomerId { get; init; } = string.Empty;

    [SwaggerSchema("Customer's email address.")]
    public string CustomerEmail { get; init; } = string.Empty;

    [SwaggerSchema("Order lifecycle status.")]
    public string Status { get; init; } = string.Empty;

    [SwaggerSchema("Order total (Subtotal + ShippingFee).")]
    public decimal Total { get; init; }

    [SwaggerSchema("Number of line items on the order.")]
    public int ItemCount { get; init; }

    [SwaggerSchema("UTC timestamp of order creation.")]
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

    /// <summary>
    /// The statuses this order may transition to next, per
    /// <see cref="MiniEcommerce.Api.Services.OrderStatusTransitions.AllowedNexts"/>.
    /// Only populated when the detail endpoint is requested with
    /// <c>?include=allowedNexts</c> — the frontend renders this array directly
    /// so the status dropdown never duplicates the server-side state machine.
    /// </summary>
    public List<string> AllowedNextStatuses { get; init; } = [];
}

[SwaggerSchema("Customer identity on an admin order detail.")]
public record AdminOrderCustomer
{
    [SwaggerSchema("Customer's ApplicationUser id.")]
    public string Id { get; init; } = string.Empty;

    [SwaggerSchema("Customer's email address.")]
    public string Email { get; init; } = string.Empty;

    [SwaggerSchema("Customer's display name.")]
    public string FullName { get; init; } = string.Empty;
}

[SwaggerSchema("A single line item on an admin order detail.")]
public record AdminOrderItemDto
{
    [SwaggerSchema("Order item id.")]
    public int Id { get; init; }

    [SwaggerSchema("Id of the referenced product variant (ADR 0003).")]
    public int ProductVariantId { get; init; }

    [SwaggerSchema("Product name snapshot at order time.")]
    public string ProductName { get; init; } = string.Empty;

    [SwaggerSchema("Unit price snapshot at order time.")]
    public decimal UnitPrice { get; init; }

    [SwaggerSchema("Quantity ordered.")]
    public int Quantity { get; init; }

    /// <summary>
    /// Live catalogue image URL (<c>Product.Images[0]</c> by <c>SortOrder</c>).
    /// v1 has no image snapshot on <c>OrderItem</c> — the snapshot contract
    /// (CONTEXT.md rule #10) covers <c>ProductName</c> and <c>UnitPrice</c>
    /// only, so the thumbnail is resolved from the current product.
    /// </summary>
    public string ImageUrl { get; init; } = string.Empty;

    /// <summary>
    /// Snapshotted line subtotal (<c>UnitPrice * Quantity</c>) computed at
    /// server-side mapping time. Kept as an <c>init</c>-only property so it
    /// survives deserialisation and re-serialisation as a fixed historical
    /// value, per the snapshot contract in <c>CONTEXT.md</c> rule #10.
    /// </summary>
    public decimal Subtotal { get; init; }
}
