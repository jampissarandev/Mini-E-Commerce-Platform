using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Admin-only endpoints for managing products: list, create, update,
/// soft/hard delete, and image upload/removal.
/// </summary>
[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;

    public AdminProductsController(
        ApplicationDbContext context,
        IImageStorage imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/products
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get a paginated list of all products (including inactive) for admin view.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminProductListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? q = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Description.ToLower().Contains(term));
        }

        query = query.OrderBy(p => p.Name);

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                Stock = p.Stock,
                IsActive = p.IsActive,
                CategoryName = p.Category.Name,
                ImageUrl = p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.Url)
                    .FirstOrDefault() ?? string.Empty,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AdminProductListItem>>.Ok(products, new Meta
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        }));
    }

    // ═══════════════════════════════════════════════════════════
    //  POST /api/admin/products
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Create a new product.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate category exists
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INVALID_CATEGORY",
                Message = $"Category with ID {request.CategoryId} was not found."
            }));
        }

        // Generate or use provided slug
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : request.Slug;

        // Check slug uniqueness
        if (await _context.Products.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            return Conflict(ApiResponse.Fail(new ApiError
            {
                Code = "SLUG_TAKEN",
                Message = $"A product with slug \"{slug}\" already exists."
            }));
        }

        var product = new Product
        {
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            CategoryId = request.CategoryId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with category for the response
        await _context.Entry(product).Reference(p => p.Category).LoadAsync(cancellationToken);

        var dto = MapToDetailDto(product);
        return CreatedAtAction(nameof(GetProducts), null, ApiResponse<AdminProductDetailDto>.Ok(dto));
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/products/:id
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Update an existing product.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProduct(
        int id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "PRODUCT_NOT_FOUND",
                Message = $"Product with ID {id} was not found."
            }));
        }

        // Validate category exists
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INVALID_CATEGORY",
                Message = $"Category with ID {request.CategoryId} was not found."
            }));
        }

        // Generate or use provided slug
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : request.Slug;

        // Check slug uniqueness (exclude current product)
        if (await _context.Products.AnyAsync(p => p.Slug == slug && p.Id != id, cancellationToken))
        {
            return Conflict(ApiResponse.Fail(new ApiError
            {
                Code = "SLUG_TAKEN",
                Message = $"A product with slug \"{slug}\" already exists."
            }));
        }

        product.Name = request.Name;
        product.Slug = slug;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        // Reload category reference for response
        await _context.Entry(product).Reference(p => p.Category).LoadAsync(cancellationToken);

        var dto = MapToDetailDto(product);
        return Ok(ApiResponse<AdminProductDetailDto>.Ok(dto));
    }

    // ═══════════════════════════════════════════════════════════
    //  DELETE /api/admin/products/:id
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Delete a product. Defaults to soft-delete (sets IsActive = false).
    /// Pass <c>?hard=true</c> for permanent deletion.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteProduct(
        int id,
        [FromQuery] bool hard = false,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "PRODUCT_NOT_FOUND",
                Message = $"Product with ID {id} was not found."
            }));
        }

        if (hard)
        {
            // Check if the product is referenced by any order item or cart item
            var inUse = await _context.OrderItems
                .AnyAsync(oi => oi.ProductId == id, cancellationToken) ||
                await _context.CartItems
                .AnyAsync(ci => ci.ProductId == id, cancellationToken);

            if (inUse)
            {
                return Conflict(ApiResponse.Fail(new ApiError
                {
                    Code = "PRODUCT_IN_USE",
                    Message = "Cannot permanently delete a product that is referenced by existing orders or cart items. Soft-delete instead."
                }));
            }

            // Remove associated images from storage
            var images = await _context.ProductImages
                .Where(pi => pi.ProductId == id)
                .ToListAsync(cancellationToken);
            foreach (var image in images)
            {
                try
                {
                    await _imageStorage.DeleteAsync(image.Url, cancellationToken);
                }
                catch
                {
                    // Best-effort: continue even if file removal fails
                }
            }

            _context.Products.Remove(product);
        }
        else
        {
            product.IsActive = false;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse.Ok());
    }

    // ═══════════════════════════════════════════════════════════
    //  POST /api/admin/products/:id/images
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Upload one or more images for a product.
    /// </summary>
    [HttpPost("{id:int}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductImageDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImages(
        int id,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "PRODUCT_NOT_FOUND",
                Message = $"Product with ID {id} was not found."
            }));
        }

        var nextSortOrder = product.Images.Count > 0
            ? product.Images.Max(i => i.SortOrder) + 1
            : 0;

        var createdImages = new List<ProductImageDto>();

        foreach (var file in files)
        {
            using var stream = file.OpenReadStream();
            var url = await _imageStorage.SaveAsync(stream, file.FileName, cancellationToken);

            var image = new ProductImage
            {
                Url = url,
                SortOrder = nextSortOrder++,
                ProductId = id
            };

            _context.ProductImages.Add(image);
            createdImages.Add(new ProductImageDto
            {
                Id = image.Id, // Will be 0 until SaveChanges — set after save
                Url = url,
                SortOrder = image.SortOrder
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Fix up IDs after save
        var savedImages = createdImages
            .Zip(product.Images.OrderByDescending(i => i.SortOrder).Take(createdImages.Count).ToList(),
                 (dto, img) => new ProductImageDto
                 {
                     Id = img.Id,
                     Url = dto.Url,
                     SortOrder = dto.SortOrder
                 })
            .ToList();

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<List<ProductImageDto>>.Ok(savedImages));
    }

    // ═══════════════════════════════════════════════════════════
    //  DELETE /api/admin/products/:id/images/:imageId
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Remove an image from a product.
    /// </summary>
    [HttpDelete("{id:int}/images/{imageId:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(
        int id,
        int imageId,
        CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(pi => pi.Id == imageId && pi.ProductId == id, cancellationToken);

        if (image is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "IMAGE_NOT_FOUND",
                Message = $"Image with ID {imageId} was not found for product {id}."
            }));
        }

        // Remove from storage (best-effort)
        try
        {
            await _imageStorage.DeleteAsync(image.Url, cancellationToken);
        }
        catch
        {
            // Continue even if file removal fails
        }

        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse.Ok());
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private static AdminProductDetailDto MapToDetailDto(Product product)
    {
        return new AdminProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            IsActive = product.IsActive,
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

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }
}
