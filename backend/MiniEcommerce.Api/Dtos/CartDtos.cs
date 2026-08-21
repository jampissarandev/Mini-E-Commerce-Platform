using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("Adds a product to the customer's cart.")]
public record AddCartItemRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "ProductVariantId must be a positive integer.")]
    [SwaggerSchema("Id of the product variant to add.")]
    public int ProductVariantId { get; init; }

    [Required]
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
    [SwaggerSchema("Quantity to add (1–100).")]
    public int Quantity { get; init; }
}

[SwaggerSchema("Updates the quantity of an existing cart item.")]
public record UpdateCartItemRequest
{
    [Required]
    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")]
    [SwaggerSchema("New quantity (1–100).")]
    public int Quantity { get; init; }
}

[SwaggerSchema("The customer's cart with its line items and computed total.")]
public record CartDto
{
    [SwaggerSchema("Cart id.")]
    public int Id { get; init; }

    [SwaggerSchema("UTC timestamp of cart creation.")]
    public DateTime CreatedAt { get; init; }

    [SwaggerSchema("UTC timestamp of the last cart update.")]
    public DateTime UpdatedAt { get; init; }

    [SwaggerSchema("Line items in the cart.")]
    public List<CartItemDto> Items { get; init; } = [];

    [SwaggerSchema("Computed total of all line items (UnitPrice × Quantity).")]
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}

[SwaggerSchema("A single line item in the cart.")]
public record CartItemDto
{
    [SwaggerSchema("Cart item id.")]
    public int Id { get; init; }

    [SwaggerSchema("Id of the referenced product variant.")]
    public int ProductVariantId { get; init; }

    [SwaggerSchema("Product name snapshot at add-time.")]
    public string ProductName { get; init; } = string.Empty;

    [SwaggerSchema("Product slug for deep links.")]
    public string ProductSlug { get; init; } = string.Empty;

    [SwaggerSchema("Variant size attribute, if any.")]
    public string? Size { get; init; }

    [SwaggerSchema("Variant color attribute, if any.")]
    public string? Color { get; init; }

    [SwaggerSchema("Primary product image URL.")]
    public string ImageUrl { get; init; } = string.Empty;

    [SwaggerSchema("Unit price snapshot at add-time.")]
    public decimal UnitPrice { get; init; }

    [SwaggerSchema("Quantity in the cart.")]
    public int Quantity { get; init; }

    [SwaggerSchema("Computed line subtotal (UnitPrice × Quantity).")]
    public decimal Subtotal => UnitPrice * Quantity;
}
