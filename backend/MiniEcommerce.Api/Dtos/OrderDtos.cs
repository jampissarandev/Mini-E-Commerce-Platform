using System.ComponentModel.DataAnnotations;

namespace MiniEcommerce.Api.Dtos;

public record CheckoutRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Street is required.")]
    [MinLength(3, ErrorMessage = "Street must be at least 3 characters.")]
    public string Street { get; init; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [MinLength(2, ErrorMessage = "City must be at least 2 characters.")]
    public string City { get; init; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [MinLength(3, ErrorMessage = "Postal code must be at least 3 characters.")]
    public string PostalCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [MinLength(2, ErrorMessage = "Country must be at least 2 characters.")]
    public string Country { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [MinLength(5, ErrorMessage = "Phone must be at least 5 characters.")]
    public string Phone { get; init; } = string.Empty;
}

public record OrderDto
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
    public List<OrderItemDto> Items { get; init; } = [];
}

public record OrderItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal Subtotal => UnitPrice * Quantity;
}
