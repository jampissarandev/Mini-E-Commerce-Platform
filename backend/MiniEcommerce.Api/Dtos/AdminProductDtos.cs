using System.ComponentModel.DataAnnotations;

namespace MiniEcommerce.Api.Dtos;

// ═══════════════════ Request DTOs ═══════════════════

public record CreateProductRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional — auto-generated from Name if omitted.</summary>
    public string? Slug { get; init; }

    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 999_999.99)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int Stock { get; init; }

    public int CategoryId { get; init; }
    public bool IsActive { get; init; } = true;
}

public record UpdateProductRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional — auto-generated from Name if omitted.</summary>
    public string? Slug { get; init; }

    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 999_999.99)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int Stock { get; init; }

    public int CategoryId { get; init; }
    public bool IsActive { get; init; } = true;
}

// ═══════════════════ Response DTOs ═══════════════════

public record AdminProductListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public record AdminProductDetailDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public ProductCategoryDto Category { get; init; } = null!;
    public List<ProductImageDto> Images { get; init; } = [];
}
