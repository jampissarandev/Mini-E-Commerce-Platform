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
    public int Stock { get; init; }
    public DateTime CreatedAt { get; init; }
    public ProductCategoryDto Category { get; init; } = null!;
    public List<ProductImageDto> Images { get; init; } = [];
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
