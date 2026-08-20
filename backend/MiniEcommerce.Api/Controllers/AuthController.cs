using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly RefreshTokenService _refreshTokens;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationDbContext context,
        RefreshTokenService refreshTokens)
    {
        _userManager = userManager;
        _configuration = configuration;
        _context = context;
        _refreshTokens = refreshTokens;
    }

    /// <summary>
    /// Register a new user account (Customer role by default).
    /// </summary>
    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register", Description = "Creates a Customer account and returns a JWT. 400 REGISTRATION_FAILED on duplicate email or weak password.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "REGISTRATION_FAILED",
                Message = "Could not create user account.",
                Details = errors
            }));
        }

        // Assign Customer role
        await _userManager.AddToRoleAsync(user, "Customer");

        var authResponse = await GenerateAuthResponseAsync(user);
        await SetRefreshCookieAsync(user.Id);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AuthResponse>.Ok(authResponse));
    }

    /// <summary>
    /// Authenticate with email + password and receive a JWT.
    /// </summary>
    [HttpPost("login")]
    [SwaggerOperation(Summary = "Login", Description = "Authenticates with email + password and returns a JWT access token. 401 INVALID_CREDENTIALS on failure.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(ApiResponse.Fail(new ApiError
            {
                Code = "INVALID_CREDENTIALS",
                Message = "Invalid email or password."
            }));
        }

        var authResponse = await GenerateAuthResponseAsync(user);
        await SetRefreshCookieAsync(user.Id);

        return Ok(ApiResponse<AuthResponse>.Ok(authResponse));
    }

    /// <summary>
    /// Refresh the access token using the httpOnly refresh cookie.
    /// Rotates: old token RevokedAt + ReplacedById → new token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Refresh", Description = "Rotates the refresh token (httpOnly cookie) and returns a new access token. 401 if cookie missing/invalid/revoked/expired.")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var raw = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(raw))
            return Unauthorized(ApiResponse.Fail(new ApiError { Code = "REFRESH_REQUIRED", Message = "Refresh token missing." }));

        var current = await _refreshTokens.FindByRawAsync(raw, ct);
        if (current is null || current.RevokedAt is not null || current.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(ApiResponse.Fail(new ApiError { Code = "REFRESH_INVALID", Message = "Refresh token invalid or expired." }));

        var rotated = await _refreshTokens.RotateAsync(current, ct);
        if (rotated is null)
            return Unauthorized(ApiResponse.Fail(new ApiError { Code = "REFRESH_INVALID", Message = "Refresh token invalid or expired." }));

        var (next, newRaw) = rotated.Value;
        AppendRefreshCookie(newRaw, next.ExpiresAt);

        var user = await _userManager.FindByIdAsync(current.CustomerId);
        if (user is null)
            return Unauthorized(ApiResponse.Fail(new ApiError { Code = "REFRESH_INVALID", Message = "User not found." }));

        var auth = await GenerateAuthResponseAsync(user);
        return Ok(ApiResponse<AuthResponse>.Ok(auth));
    }

    /// <summary>
    /// Revoke the active refresh token and clear the cookie.
    /// </summary>
    [HttpPost("logout")]
    [SwaggerOperation(Summary = "Logout", Description = "Revokes the active refresh token (from cookie or Authorization header) and clears the cookie.")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Prefer cookie; fall back to revoking the caller's latest active token via identity if cookie absent
        var raw = Request.Cookies["refresh_token"];
        if (!string.IsNullOrEmpty(raw))
        {
            var token = await _refreshTokens.FindByRawAsync(raw, ct);
            if (token is not null && token.RevokedAt is null)
                await _refreshTokens.RevokeAsync(token, ct);
        }
        else
        {
            var uid = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(uid))
            {
                var active = await _context.RefreshTokens
                    .Where(x => x.CustomerId == uid && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (active is not null)
                    await _refreshTokens.RevokeAsync(active, ct);
            }
        }

        Response.Cookies.Delete("refresh_token", new CookieOptions { Path = "/api/auth" });
        // Also clear with root path for clients that stored it there
        Response.Cookies.Delete("refresh_token");
        return Ok(ApiResponse<object>.Ok(new { message = "Logged out" }));
    }

    /// <summary>
    /// Admin-only smoke endpoint used to verify role-gated authorization.
    /// Returns 200 for Admin tokens, 403 for Customer tokens (via the
    /// JwtBearer OnForbidden event in Program.cs).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/ping")]
    [SwaggerOperation(Summary = "Admin ping", Description = "Verifies Admin role. 401 if unauthenticated, 403 if not Admin.")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult AdminPing()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            message = "pong",
            role = "Admin"
        }));
    }

    /// <summary>
    /// Get the currently authenticated user's profile.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [SwaggerOperation(Summary = "Me", Description = "Returns the currently authenticated user's profile. 401 if not authenticated.")]
    [ProducesResponseType(typeof(ApiResponse<CustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Customer";

        var customerDto = new CustomerDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = role,
            CreatedAt = user.CreatedAt
        };

        return Ok(ApiResponse<CustomerDto>.Ok(customerDto));
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Customer";

        var token = GenerateJwtToken(user, role);
        var expiresAt = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60));

        return new AuthResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            Customer = new CustomerDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Role = role,
                CreatedAt = user.CreatedAt
            }
        };
    }

    private string GenerateJwtToken(ApplicationUser user, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("fullname", user.FullName),
            // Emit the standard ClaimTypes.Role so that after JwtSecurityTokenHandler
            // remaps it, the principal has a role claim whose Type matches the
            // RoleClaimType configured in Program.cs.
            new(System.Security.Claims.ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                _configuration.GetValue<int>("Jwt:ExpiresInMinutes", 60)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task SetRefreshCookieAsync(string customerId)
    {
        var (_, raw) = await _refreshTokens.IssueAsync(customerId);
        AppendRefreshCookie(raw, DateTime.UtcNow.AddDays(_refreshTokens.RefreshExpiresInDays));
    }

    private void AppendRefreshCookie(string raw, DateTime expiresAt)
    {
        Response.Cookies.Append("refresh_token", raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt
        });
    }
}
