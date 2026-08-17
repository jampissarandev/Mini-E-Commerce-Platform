using Microsoft.AspNetCore.Mvc;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    [HttpGet("api/health")]
    public IActionResult Get()
    {
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
