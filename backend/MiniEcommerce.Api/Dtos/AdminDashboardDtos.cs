namespace MiniEcommerce.Api.Dtos;

/// <summary>
/// Headline metrics for the admin dashboard KPI cards (Task 17a).
/// Every value is computed server-side from the live database so the
/// frontend renders the cards without N round-trips.
/// </summary>
public record DashboardSummary
{
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalProducts { get; init; }
    public int LowStockCount { get; init; }
}

/// <summary>
/// One day in the daily sales series (Task 17a). <see cref="Date"/> is an
/// ISO-8601 date string (<c>yyyy-MM-dd</c>, UTC) so the frontend chart can
/// label the axis without timezone ambiguity.
/// </summary>
public record SalesPoint
{
    public string Date { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public int OrderCount { get; init; }
}

/// <summary>
/// A product flagged for restocking (Task 17a). v1 has no variants
/// (ADR 0003 is Phase 7), so stock lives on <see cref="Models.Product"/>.
/// </summary>
public record LowStockProductDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Stock { get; init; }
}