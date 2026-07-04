using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniEcommerce.Api.Dtos;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Stub admin-only controller. Real admin endpoints (products, orders)
/// land in Phase 5–6. This exists solely to verify role-based access
/// at the API level for the Auth checkpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    /// <summary>
    /// Lightweight endpoint that only Admin users can reach.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult GetDashboard()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            message = "Welcome, Admin.",
            timestamp = DateTime.UtcNow
        }));
    }
}
