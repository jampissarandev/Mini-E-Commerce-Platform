using System.ComponentModel.DataAnnotations;

namespace MiniEcommerce.Api.Dtos;

public record CheckoutRequest
{
    [Required(ErrorMessage = "Shipping address is required.")]
    [MinLength(3, ErrorMessage = "Shipping address must be at least 3 characters.")]
    public string ShippingAddress { get; init; } = string.Empty;
}

public record OrderDto
{
    public int Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal Total { get; init; }
    public string ShippingAddress { get; init; } = string.Empty;
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
