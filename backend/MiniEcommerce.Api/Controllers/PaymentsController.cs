using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Services;

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

public record MockPaymentModeDto
{
    public string Mode { get; init; } = "AlwaysSucceed";
    public decimal? FailIfAmountGreaterThan { get; init; }
}
