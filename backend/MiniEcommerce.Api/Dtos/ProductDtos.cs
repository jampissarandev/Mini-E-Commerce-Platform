namespace MiniEcommerce.Api.Dtos;

public record ProductListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
}

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

public record ProductCategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
}

public record ProductImageDto
{
    public int Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public int ProductCount { get; init; }
}
