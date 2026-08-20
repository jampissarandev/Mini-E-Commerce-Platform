using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Exposes the current mock payment mode so the frontend can show a banner
/// when failure-injection is active. This is intentionally a no-auth endpoint
/// because it only returns non-sensitive configuration that is already known
/// to whoever flipped the mode in their local dev environment.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly MockPaymentOptions _options;

    public PaymentsController(IOptions<MockPaymentOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Returns the currently active mock payment mode and threshold. Useful
    /// for the checkout UI to display a "this checkout will fail" banner
    /// during local demos.
    /// </summary>
    [HttpGet("mock-mode")]
    [ProducesResponseType(typeof(ApiResponse<MockPaymentModeDto>), StatusCodes.Status200OK)]
    public IActionResult GetMockMode()
    {
        var dto = new MockPaymentModeDto
        {
            Mode = _options.Mode.ToString(),
            FailIfAmountGreaterThan = _options.Mode == MockPaymentMode.FailIfAmountGreaterThan
                ? _options.FailIfAmountGreaterThan
                : null,
        };
        return Ok(ApiResponse<MockPaymentModeDto>.Ok(dto));
    }
}

[SwaggerSchema("The currently active mock payment mode and threshold.")]
public record MockPaymentModeDto
{
    [SwaggerSchema("Mock payment mode: AlwaysSucceed, AlwaysFail, or FailIfAmountGreaterThan.")]
    public string Mode { get; init; } = "AlwaysSucceed";

    [SwaggerSchema("Amount threshold for FailIfAmountGreaterThan mode, or null otherwise.")]
    public decimal? FailIfAmountGreaterThan { get; init; }
}
