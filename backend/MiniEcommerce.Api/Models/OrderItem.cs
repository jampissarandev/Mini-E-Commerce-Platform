namespace MiniEcommerce.Api.Models;

public class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    /// <summary>
    /// Snapshotted variant size at order time (ADR 0008). Null when the
    /// chosen variant had no <c>Size</c> attribute (e.g. legacy backfilled
    /// variants from the <c>AddProductVariants</c> migration).
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// Snapshotted variant color at order time (ADR 0008). Null when the
    /// chosen variant had no <c>Color</c> attribute.
    /// </summary>
    public string? Color { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
}
