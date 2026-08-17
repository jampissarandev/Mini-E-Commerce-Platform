using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Admin-only dashboard endpoints (Task 17): headline summary (17a),
/// daily sales series, recent orders, and low-stock products.
///
/// Role gating is on each action explicitly (not the class) per the
/// project standard in <c>CONTEXT.md</c> rule #4.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    /// <summary>
    /// Default low-stock threshold used by the summary's <c>LowStockCount</c>
    /// and the <c>GET /low-stock</c> endpoint when no threshold is supplied.
    /// </summary>
    public const int DefaultLowStockThreshold = 10;

    private readonly ApplicationDbContext _context;

    public AdminDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/summary  (ticket 17a)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Headline metrics for the KPI cards: total orders, total revenue
    /// (sum of <c>Order.Total</c>), total customers (users in the
    /// <c>Customer</c> role), total products, and low-stock count.
    /// </summary>
    /// <param name="cancellationToken"></param>
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DashboardSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        var totalOrders = await _context.Orders.CountAsync(cancellationToken);
        var totalRevenue = await _context.Orders
            .SumAsync(o => (decimal?)o.Total, cancellationToken) ?? 0m;
        var totalProducts = await _context.Products.CountAsync(cancellationToken);
        var lowStockCount = await _context.Products
            .CountAsync(p => p.Stock <= DefaultLowStockThreshold, cancellationToken);

        // "Customer" is the canonical term for the person who places orders
        // (CONTEXT.md) — count users in the Customer role, not every user.
        var customerRoleId = await _context.Roles
            .Where(r => r.Name == "Customer")
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var totalCustomers = await _context.UserRoles
            .CountAsync(ur => ur.RoleId == customerRoleId, cancellationToken);

        return Ok(ApiResponse<DashboardSummary>.Ok(new DashboardSummary
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            TotalCustomers = totalCustomers,
            TotalProducts = totalProducts,
            LowStockCount = lowStockCount
        }));
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/sales?days=30  (ticket 17a)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Daily sales series for the last <paramref name="days"/> calendar days
    /// (UTC). Always returns exactly <paramref name="days"/> points — days
    /// with no orders are zero-filled so the chart axis is continuous.
    /// </summary>
    /// <param name="days">Number of calendar days to include (default 30, clamped to [1, 90]).</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("sales")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<SalesPoint>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSales(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (days < 1) days = 30;
        if (days > 90) days = 90;

        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var grouped = await _context.Orders
            .Where(o => o.CreatedAt >= start)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(o => o.Total),
                OrderCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        var byDate = grouped.ToDictionary(g => g.Date, g => g);

        var series = Enumerable.Range(0, days)
            .Select(i => start.AddDays(i))
            .Select(d => byDate.TryGetValue(d, out var point)
                ? new SalesPoint
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Total = point.Total,
                    OrderCount = point.OrderCount
                }
                : new SalesPoint
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Total = 0m,
                    OrderCount = 0
                })
            .ToList();

        return Ok(ApiResponse<List<SalesPoint>>.Ok(series));
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/recent-orders?limit=10  (ticket 17a)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// The latest orders, newest-first, reusing the existing
    /// <see cref="AdminOrderListItem"/> shape so the dashboard list and the
    /// orders table share one projection (snapshot is the truth).
    /// </summary>
    /// <param name="limit">Max rows to return (default 10, clamped to [1, 50]).</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("recent-orders")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<AdminOrderListItem>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentOrders(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1) limit = 10;
        if (limit > 50) limit = 50;

        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Take(limit)
            .Select(o => AdminOrderMapping.ToListItem(o))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AdminOrderListItem>>.Ok(orders));
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/low-stock?threshold=10  (ticket 17a)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Products with <c>Stock &lt;= threshold</c>, ordered by stock ascending
    /// then name, so the most urgent restocks appear first.
    /// </summary>
    /// <param name="threshold">Stock ceiling (default 10, clamped to >= 0).</param>
    /// <param name="cancellationToken"></param>
    [HttpGet("low-stock")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<LowStockProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(
        [FromQuery] int threshold = DefaultLowStockThreshold,
        CancellationToken cancellationToken = default)
    {
        if (threshold < 0) threshold = 0;

        var products = await _context.Products
            .Where(p => p.Stock <= threshold)
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Name)
            .Select(p => new LowStockProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Stock = p.Stock
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<LowStockProductDto>>.Ok(products));
    }
}