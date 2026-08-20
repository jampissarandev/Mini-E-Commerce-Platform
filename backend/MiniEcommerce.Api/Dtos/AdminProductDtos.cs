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

    [Range(0, int.MaxValue)]
    [SwaggerSchema("Available stock quantity.")]
    public int Stock { get; init; }

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

    [Range(0, int.MaxValue)]
    [SwaggerSchema("Available stock quantity.")]
    public int Stock { get; init; }

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

    [SwaggerSchema("Available stock quantity.")]
    public int Stock { get; init; }

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

    [SwaggerSchema("Available stock quantity.")]
    public int Stock { get; init; }

    [SwaggerSchema("Whether the product is visible in the public catalog.")]
    public bool IsActive { get; init; }

    [SwaggerSchema("UTC timestamp of product creation.")]
    public DateTime CreatedAt { get; init; }

    [SwaggerSchema("The product's category.")]
    public ProductCategoryDto Category { get; init; } = null!;

    [SwaggerSchema("Product images, ordered by SortOrder.")]
    public List<ProductImageDto> Images { get; init; } = [];
}
