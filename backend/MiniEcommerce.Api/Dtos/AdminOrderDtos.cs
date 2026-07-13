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
