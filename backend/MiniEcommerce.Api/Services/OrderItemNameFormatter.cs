using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Formats the snapshot product name written to <c>OrderItem.ProductName</c>
/// at checkout. Pinned by CONTEXT.md → OrderItem and ADR 0003 §Consequences:
/// "Product Name" for no-attribute variants,
/// "Product Name (Color)" for color only,
/// "Product Name (Color, Size)" for both, with Color before Size.
///
/// Extracted as a public static helper so the format can be pinned by unit
/// tests (the previous private implementation in <c>OrdersController</c> was
/// only reachable through the full HTTP checkout flow).
/// </summary>
public static class OrderItemNameFormatter
{
    public static string Format(Product product, ProductVariant variant)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(variant.Color)) parts.Add(variant.Color);
        if (!string.IsNullOrEmpty(variant.Size)) parts.Add(variant.Size);

        return parts.Count > 0
            ? $"{product.Name} ({string.Join(", ", parts)})"
            : product.Name;
    }
}
