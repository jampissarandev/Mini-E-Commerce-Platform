using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Admin-only endpoints for managing orders: list, detail (15b), and
/// status transitions (15c). Mounted at <c>/api/admin/orders</c>.
/// </summary>
[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminOrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders  (ticket 15a)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Get a paginated list of every order in the system, with optional
    /// filtering by status, date range, and free-text search.
    /// </summary>
    /// <param name="page">Page number (default 1, clamped to >= 1).</param>
    /// <param name="pageSize">Items per page (default 20, clamped to [1, 100]).</param>
    /// <param name="status">Exact match on the OrderStatus enum string (e.g. "Paid").</param>
    /// <param name="q">Free-text search: matches customer email (case-insensitive) or order id (numeric).</param>
    /// <param name="from">ISO-8601 date (inclusive lower bound, UTC midnight).</param>
    /// <param name="to">ISO-8601 date (exclusive upper bound at day level: to=2026-07-13 means before 2026-07-14 00:00 UTC).</param>
    /// <param name="cancellationToken"></param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminOrderListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? q = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Validate status filter
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            {
                statusFilter = parsed;
            }
            else
            {
                return BadRequest(ApiResponse.Fail(new ApiError
                {
                    Code = "INVALID_STATUS",
                    Message = $"'{status}' is not a valid OrderStatus value."
                }));
            }
        }

        // Parse date range (from/to are date-only strings, interpreted as UTC midnight)
        DateTime? fromDate = null;
        DateTime? toDateExclusive = null;
        if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var parsedFrom))
        {
            fromDate = parsedFrom.Date;
        }
        if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var parsedTo))
        {
            // to is exclusive at day level: to=2026-07-13 means before 2026-07-14 00:00 UTC
            toDateExclusive = parsedTo.Date.AddDays(1);
        }

        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .AsQueryable();

        // Filter by status
        if (statusFilter.HasValue)
        {
            query = query.Where(o => o.Status == statusFilter.Value);
        }

        // Filter by date range [from, to+1day)
        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }
        if (toDateExclusive.HasValue)
        {
            query = query.Where(o => o.CreatedAt < toDateExclusive.Value);
        }

        // Free-text search: match customer email or order id
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            if (int.TryParse(term, out var orderId))
            {
                // Search by order ID
                query = query.Where(o => o.Id == orderId);
            }
            else
            {
                // Search by customer email (case-insensitive)
                query = query.Where(o =>
                    o.Customer.Email != null &&
                    o.Customer.Email.ToLower().Contains(term.ToLower()));
            }
        }

        // Order: newest first, stable tie-break on Id
        query = query.OrderByDescending(o => o.CreatedAt)
                      .ThenByDescending(o => o.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderListItem
            {
                Id = o.Id,
                CustomerId = o.CustomerId,
                CustomerEmail = o.Customer.Email ?? string.Empty,
                Status = o.Status.ToString(),
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity),
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AdminOrderListItem>>.Ok(orders, new Meta
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        }));
    }
}
