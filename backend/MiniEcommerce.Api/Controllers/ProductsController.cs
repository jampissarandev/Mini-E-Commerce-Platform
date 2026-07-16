using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Public catalog endpoints: list active products and fetch a single
/// product's detail. Mounted at <c>/api/products</c>. Anonymous access —
/// auth lives only on the <c>/api/admin/products</c> surface.
/// </summary>
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get a paginated list of <i>active</i> products for the public
    /// catalog. Inactive products (soft-deleted) are hidden, per the
    /// <c>soft delete by default</c> rule in <c>CONTEXT.md</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ProductListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                CategoryName = p.Category.Name,
                ImageUrl = p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault() ?? string.Empty,
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<ProductListItem>>.Ok(products, new Meta
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        }));
    }

    /// <summary>
    /// Get the full detail of a single active product by id. Returns 404
    /// for missing or inactive products so deleted SKUs aren't leaked via
    /// direct URL access.
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
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(
                $"Product with id {id} was not found.",
                code: "PRODUCT_NOT_FOUND");
        }

        return Ok(ApiResponse<ProductDetailDto>.Ok(MapToDetailDto(product)));
    }

    private static ProductDetailDto MapToDetailDto(Product product)
    {
        return new ProductDetailDto
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
            Images = product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    SortOrder = i.SortOrder
                })
                .ToList()
        };
    }
}
