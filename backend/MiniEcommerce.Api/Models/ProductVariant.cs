namespace MiniEcommerce.Api.Models;

/// <summary>
/// A sellable unit belonging to a <see cref="Product"/>. A Product has 1..n
/// Variants; the cart and order reference Variants, not Products.
/// Each variant carries its own SKU (unique), optional Size/Color, and Stock.
/// </summary>
public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>Unique stock-keeping unit identifier (e.g. "TS-BLK-M").</summary>
    public string Sku { get; set; } = string.Empty;

    /// <summary>Optional size attribute (e.g. "S", "M", "L").</summary>
    public string? Size { get; set; }

    /// <summary>Optional color attribute (e.g. "Black", "White").</summary>
    public string? Color { get; set; }

    /// <summary>Available stock for this specific variant.</summary>
    public int Stock { get; set; }

    /// <summary>Soft-delete flag. Inactive variants are hidden from the catalog.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
