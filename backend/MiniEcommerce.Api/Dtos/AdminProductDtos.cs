using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Request DTOs ═══════════════════

[SwaggerSchema("Creates a new product in the catalog.")]
public record CreateProductRequest
{
    [Required]
    [MaxLength(200)]
    [SwaggerSchema("Product display name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional — auto-generated from Name if omitted.</summary>
    [SwaggerSchema("URL-safe identifier. Auto-generated from Name if omitted.")]
    public string? Slug { get; init; }

    [Required]
    [SwaggerSchema("Long-form product description.")]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 999_999.99)]
    [SwaggerSchema("Unit price in USD.")]
    public decimal Price { get; init; }

    [SwaggerSchema("Id of the category this product belongs to.")]
    public int CategoryId { get; init; }

    [SwaggerSchema("Whether the product is visible in the public catalog.")]
    public bool IsActive { get; init; } = true;
}

[SwaggerSchema("Updates an existing product in the catalog.")]
public record UpdateProductRequest
{
    [Required]
    [MaxLength(200)]
    [SwaggerSchema("Product display name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional — auto-generated from Name if omitted.</summary>
    [SwaggerSchema("URL-safe identifier. Auto-generated from Name if omitted.")]
    public string? Slug { get; init; }

    [Required]
    [SwaggerSchema("Long-form product description.")]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 999_999.99)]
    [SwaggerSchema("Unit price in USD.")]
    public decimal Price { get; init; }

    [SwaggerSchema("Id of the category this product belongs to.")]
    public int CategoryId { get; init; }

    [SwaggerSchema("Whether the product is visible in the public catalog.")]
    public bool IsActive { get; init; } = true;
}

// ═══════════════════ Response DTOs ═══════════════════

[SwaggerSchema("Compact product row for the admin product table.")]
public record AdminProductListItem
{
    [SwaggerSchema("Product id.")]
    public int Id { get; init; }

    [SwaggerSchema("Product display name.")]
    public string Name { get; init; } = string.Empty;

    [SwaggerSchema("URL-safe identifier.")]
    public string Slug { get; init; } = string.Empty;

    [SwaggerSchema("Unit price in USD.")]
    public decimal Price { get; init; }

    [SwaggerSchema("Total stock across all active variants.")]
    public int TotalStock { get; init; }

    [SwaggerSchema("Number of active variants.")]
    public int VariantCount { get; init; }

    [SwaggerSchema("Whether the product is visible in the public catalog.")]
    public bool IsActive { get; init; }

    [SwaggerSchema("Category display name.")]
    public string CategoryName { get; init; } = string.Empty;

    [SwaggerSchema("Primary product image URL, or empty.")]
    public string ImageUrl { get; init; } = string.Empty;

    [SwaggerSchema("UTC timestamp of product creation.")]
    public DateTime CreatedAt { get; init; }
}

[SwaggerSchema("Full product payload for the admin edit form.")]
public record AdminProductDetailDto
{
    [SwaggerSchema("Product id.")]
    public int Id { get; init; }

    [SwaggerSchema("Product display name.")]
    public string Name { get; init; } = string.Empty;

    [SwaggerSchema("URL-safe identifier.")]
    public string Slug { get; init; } = string.Empty;

    [SwaggerSchema("Long-form product description.")]
    public string Description { get; init; } = string.Empty;

    [SwaggerSchema("Unit price in USD.")]
    public decimal Price { get; init; }

    [SwaggerSchema("Whether the product is visible in the public catalog.")]
    public bool IsActive { get; init; }

    [SwaggerSchema("UTC timestamp of product creation.")]
    public DateTime CreatedAt { get; init; }

    [SwaggerSchema("The product's category.")]
    public ProductCategoryDto Category { get; init; } = null!;

    [SwaggerSchema("Product images, ordered by SortOrder.")]
    public List<ProductImageDto> Images { get; init; } = [];

    [SwaggerSchema("Product variants with stock and attributes.")]
    public List<ProductVariantDto> Variants { get; init; } = [];
}

// ═══════════════════ Variant Management DTOs ═══════════════════

[SwaggerSchema("Creates a new variant for a product.")]
public record CreateVariantRequest
{
    [Required]
    [MaxLength(100)]
    [SwaggerSchema("Unique stock-keeping unit identifier.")]
    public string Sku { get; init; } = string.Empty;

    [SwaggerSchema("Optional size attribute (e.g. S, M, L).")]
    public string? Size { get; init; }

    [SwaggerSchema("Optional color attribute (e.g. Black, White).")]
    public string? Color { get; init; }

    [Range(0, int.MaxValue)]
    [SwaggerSchema("Available stock for this variant.")]
    public int Stock { get; init; }

    [SwaggerSchema("Whether this variant is visible in the catalog.")]
    public bool IsActive { get; init; } = true;
}

[SwaggerSchema("Updates an existing variant.")]
public record UpdateVariantRequest
{
    [Required]
    [MaxLength(100)]
    [SwaggerSchema("Unique stock-keeping unit identifier.")]
    public string Sku { get; init; } = string.Empty;

    [SwaggerSchema("Optional size attribute.")]
    public string? Size { get; init; }

    [SwaggerSchema("Optional color attribute.")]
    public string? Color { get; init; }

    [Range(0, int.MaxValue)]
    [SwaggerSchema("Available stock for this variant.")]
    public int Stock { get; init; }

    [SwaggerSchema("Whether this variant is visible in the catalog.")]
    public bool IsActive { get; init; } = true;
}
