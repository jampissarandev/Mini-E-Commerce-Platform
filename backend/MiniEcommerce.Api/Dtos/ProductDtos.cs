namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Response DTOs ═══════════════════

/// <summary>
/// Compact product payload for the public catalog grid. Mirrors the
/// TypeScript <c>ProductListItem</c> in <c>frontend/src/lib/types.ts</c>.
/// </summary>
public record ProductListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }

    /// <summary>URL of the product's primary (lowest SortOrder) image, or empty.</summary>
    public string ImageUrl { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    /// <summary>
    /// Number of active variants for this product. Task 27b: the public
    /// catalog list surfaces variant count so the grid can show "T-Shirt —
    /// 6 options" without fetching each detail. The full variant list (size,
    /// color, SKU, stock) is only on the detail endpoint to keep the list
    /// payload small.
    /// </summary>
    public int VariantCount { get; init; }

    /// <summary>
    /// Minimal active-variant summary ({id, stock}, Id ascending) so the
    /// frontend card can quick-add the preferred variant or fall back to
    /// navigating to the detail page when everything is out of stock
    /// (ADR 0008 Phase C). Deliberately omits Sku/Size/Color — those stay
    /// detail-only per the Task 27b payload decision.
    /// </summary>
    public List<ProductVariantSummaryDto> Variants { get; init; } = [];
}

/// <summary>
/// Minimal sellable-unit summary for the catalog list — just enough for the
/// quick-add affordance: which variant to add and whether it has stock.
/// </summary>
public record ProductVariantSummaryDto
{
    public int Id { get; init; }
    public int Stock { get; init; }
}

/// <summary>
/// Full product payload for the public product detail page. Mirrors the
/// TypeScript <c>ProductDetailDto</c> in <c>frontend/src/lib/types.ts</c>.
/// </summary>
public record ProductDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime CreatedAt { get; init; }
    public ProductCategoryDto Category { get; init; } = null!;
    public List<ProductImageDto> Images { get; init; } = [];
    public List<ProductVariantDto> Variants { get; init; } = [];
}

/// <summary>
/// Sellable unit payload — one per product variant. Carries its own stock,
/// SKU, and optional attributes. Mirrors ADR 0003 (Task 27).
/// </summary>
public record ProductVariantDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string? Size { get; init; }
    public string? Color { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Nested category payload shared by the public and admin product detail
/// surfaces. Mirrors the TypeScript <c>ProductCategoryDto</c> in
/// <c>frontend/src/lib/types.ts</c> so the JSON shape is identical across
/// the wire.
/// </summary>
public record ProductCategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}

/// <summary>
/// Category payload for the public catalog filter. Mirrors the TypeScript
/// <c>CategoryDto</c> in <c>frontend/src/lib/types.ts</c>.
/// </summary>
public record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;

    /// <summary>Number of <i>active</i> products in the category.</summary>
    public int ProductCount { get; init; }
}

/// <summary>
/// Nested product-image payload shared by the public and admin product
/// detail surfaces, and the image-upload response. Mirrors the TypeScript
/// <c>ProductImageDto</c> in <c>frontend/src/lib/types.ts</c>.
/// </summary>
public record ProductImageDto
{
    public int Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
