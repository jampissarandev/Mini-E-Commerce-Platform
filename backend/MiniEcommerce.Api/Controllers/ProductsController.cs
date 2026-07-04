using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get a paginated, filterable, sortable list of products.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ProductListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        CancellationToken cancellationToken = default)
    {
        // Clamp pageSize to a sane upper bound to prevent unbounded queries.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Products
            .Where(p => p.IsActive)
            .AsQueryable();

        // Filter by category slug
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category.Slug == category);
        }

        // Search by name or description
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term));
        }

        // Price range filter
        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        // Sorting
        query = sort?.ToLower() switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderBy(p => p.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? string.Empty,
                CategoryName = p.Category.Name
            })
            .ToListAsync(cancellationToken);

        var meta = new Meta
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        // Public catalog data is safe to cache at the edge for 60 s.
        Response.Headers.CacheControl = "public, max-age=60";

        return Ok(ApiResponse<List<ProductListItem>>.Ok(products, meta));
    }

    /// <summary>
    /// Get a single product by its ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException($"Product with ID {id} not found.");
        }

        var dto = new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            CreatedAt = product.CreatedAt,
            Category = new ProductCategoryDto
            {
                Id = product.Category.Id,
                Name = product.Category.Name,
                Slug = product.Category.Slug
            },
            Images = product.Images.Select(i => new ProductImageDto
            {
                Id = i.Id,
                Url = i.Url,
                SortOrder = i.SortOrder
            }).ToList()
        };

        return Ok(ApiResponse<ProductDetailDto>.Ok(dto));
    }
}
