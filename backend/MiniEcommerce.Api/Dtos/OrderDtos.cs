using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("Checkout payload. Converts the customer's cart into an order.")]
public record CheckoutRequest : IValidatableObject
{
    [SwaggerSchema("Optional address book id. If provided, the address fields are snapshotted from the saved address and the body fields are ignored.")]
    public int? AddressId { get; init; }

    [SwaggerSchema("Recipient full name (snapshotted onto the order). Required unless AddressId is set.")]
    public string FullName { get; init; } = string.Empty;

    [SwaggerSchema("Street address (snapshotted onto the order). Required unless AddressId is set.")]
    public string Street { get; init; } = string.Empty;

    [SwaggerSchema("City (snapshotted onto the order). Required unless AddressId is set.")]
    public string City { get; init; } = string.Empty;

    [SwaggerSchema("Postal code (snapshotted onto the order). Required unless AddressId is set.")]
    public string PostalCode { get; init; } = string.Empty;

    [SwaggerSchema("Country (snapshotted onto the order). Required unless AddressId is set.")]
    public string Country { get; init; } = string.Empty;

    [SwaggerSchema("Contact phone (snapshotted onto the order). Required unless AddressId is set.")]
    public string Phone { get; init; } = string.Empty;

    /// <summary>
    /// Shipping fields are required when the customer did not pick a saved
    /// address (<see cref="AddressId"/> is null). When <see cref="AddressId"/>
    /// is set the snapshot path in <c>OrdersController.Checkout</c> loads
    /// these from the saved address and ignores the body fields, so they
    /// must remain optional.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AddressId.HasValue) yield break;

        if (string.IsNullOrWhiteSpace(FullName) || FullName.Length < 2)
            yield return new ValidationResult("Full name is required.", new[] { nameof(FullName) });
        if (string.IsNullOrWhiteSpace(Street) || Street.Length < 3)
            yield return new ValidationResult("Street is required.", new[] { nameof(Street) });
        if (string.IsNullOrWhiteSpace(City) || City.Length < 2)
            yield return new ValidationResult("City is required.", new[] { nameof(City) });
        if (string.IsNullOrWhiteSpace(PostalCode) || PostalCode.Length < 3)
            yield return new ValidationResult("Postal code is required.", new[] { nameof(PostalCode) });
        if (string.IsNullOrWhiteSpace(Country) || Country.Length < 2)
            yield return new ValidationResult("Country is required.", new[] { nameof(Country) });
        if (string.IsNullOrWhiteSpace(Phone) || Phone.Length < 5)
            yield return new ValidationResult("Phone is required.", new[] { nameof(Phone) });
    }
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
