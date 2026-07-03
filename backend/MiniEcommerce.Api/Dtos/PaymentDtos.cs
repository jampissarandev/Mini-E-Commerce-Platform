namespace MiniEcommerce.Api.Dtos;

public record PaymentRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public Guid OrderId { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public record PaymentResult
{
    public bool Success { get; init; }
    public string? TransactionId { get; init; }
    public string? Message { get; init; }
    public PaymentStatus Status { get; init; }
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    Refunded
}
