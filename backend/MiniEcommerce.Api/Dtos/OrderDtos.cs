using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("Checkout payload. Converts the customer's cart into an order.")]
public record CheckoutRequest
{
    [SwaggerSchema("Optional address book id. If provided, the address fields are snapshotted from the saved address and the body fields are ignored.")]
    public int? AddressId { get; init; }

    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    [SwaggerSchema("Recipient full name (snapshotted onto the order). Ignored if AddressId is set.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Street is required.")]
    [MinLength(3, ErrorMessage = "Street must be at least 3 characters.")]
    [SwaggerSchema("Street address (snapshotted onto the order).")]
    public string Street { get; init; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [MinLength(2, ErrorMessage = "City must be at least 2 characters.")]
    [SwaggerSchema("City (snapshotted onto the order).")]
    public string City { get; init; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [MinLength(3, ErrorMessage = "Postal code must be at least 3 characters.")]
    [SwaggerSchema("Postal code (snapshotted onto the order).")]
    public string PostalCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [MinLength(2, ErrorMessage = "Country must be at least 2 characters.")]
    [SwaggerSchema("Country (snapshotted onto the order).")]
    public string Country { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [MinLength(5, ErrorMessage = "Phone must be at least 5 characters.")]
    [SwaggerSchema("Contact phone (snapshotted onto the order).")]
    public string Phone { get; init; } = string.Empty;
}

[SwaggerSchema("Order payload returned to the customer.")]
public record OrderDto
{
    [SwaggerSchema("Order id.")]
    public int Id { get; init; }

    [SwaggerSchema("Order lifecycle status (Pending, Paid, Shipped, Delivered, Cancelled).")]
    public string Status { get; init; } = string.Empty;

    [SwaggerSchema("Sum of line-item subtotals at checkout time.")]
    public decimal Subtotal { get; init; }

    [SwaggerSchema("Config-driven shipping fee.")]
    public decimal ShippingFee { get; init; }

    [SwaggerSchema("Subtotal + ShippingFee.")]
    public decimal Total { get; init; }

    [SwaggerSchema("Shipping snapshot — full name at order time.")]
    public string ShippingFullName { get; init; } = string.Empty;

    [SwaggerSchema("Shipping snapshot — street at order time.")]
    public string ShippingStreet { get; init; } = string.Empty;

    [SwaggerSchema("Shipping snapshot — city at order time.")]
    public string ShippingCity { get; init; } = string.Empty;

    [SwaggerSchema("Shipping snapshot — postal code at order time.")]
    public string ShippingPostalCode { get; init; } = string.Empty;

    [SwaggerSchema("Shipping snapshot — country at order time.")]
    public string ShippingCountry { get; init; } = string.Empty;

    [SwaggerSchema("Shipping snapshot — phone at order time.")]
    public string ShippingPhone { get; init; } = string.Empty;

    [SwaggerSchema("UTC timestamp of order creation.")]
    public DateTime CreatedAt { get; init; }

    [SwaggerSchema("Order line items (snapshotted).")]
    public List<OrderItemDto> Items { get; init; } = [];
}

[SwaggerSchema("A single line item on an order (snapshotted at checkout).")]
public record OrderItemDto
{
    [SwaggerSchema("Order item id.")]
    public int Id { get; init; }

    [SwaggerSchema("Id of the referenced product.")]
    public int ProductId { get; init; }

    [SwaggerSchema("Product name snapshot at order time.")]
    public string ProductName { get; init; } = string.Empty;

    [SwaggerSchema("Unit price snapshot at order time.")]
    public decimal UnitPrice { get; init; }

    [SwaggerSchema("Quantity ordered.")]
    public int Quantity { get; init; }

    [SwaggerSchema("Computed line subtotal (UnitPrice × Quantity).")]
    public decimal Subtotal => UnitPrice * Quantity;
}
