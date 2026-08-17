using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Public catalog endpoint for category filtering. Mounted at
/// <c>/api/categories</c>. Anonymous access — auth lives only on the
/// <c>/api/admin</c> surface.
/// </summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// List all categories with their <i>active</i>-product counts, ordered
    /// by name. The count mirrors what the public catalog actually shows
    /// (inactive/soft-deleted products are hidden, per the <c>soft delete by
    /// default</c> rule in <c>CONTEXT.md</c>).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ProductCount = c.Products.Count(p => p.IsActive),
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<CategoryDto>>.Ok(categories));
    }
}