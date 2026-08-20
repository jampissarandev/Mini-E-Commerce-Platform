using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("Registration payload. Creates a Customer account and returns a JWT.")]
public record RegisterRequest
{
    [Required]
    [EmailAddress]
    [SwaggerSchema("Email address used to log in. Must be unique.")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(6)]
    [SwaggerSchema("Password. Min 6 chars, at least one digit and one uppercase letter.")]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [SwaggerSchema("Display name shown in the UI.")]
    public string FullName { get; init; } = string.Empty;
}

[SwaggerSchema("Login payload. Returns a JWT access token on success.")]
public record LoginRequest
{
    [Required]
    [EmailAddress]
    [SwaggerSchema("Registered email address.")]
    public string Email { get; init; } = string.Empty;

    [Required]
    [SwaggerSchema("Account password.")]
    public string Password { get; init; } = string.Empty;
}

[SwaggerSchema("Successful authentication response: a JWT access token plus the customer profile.")]
public record AuthResponse
{
    [SwaggerSchema("JWT access token. Send as `Authorization: Bearer <token>`.")]
    public string Token { get; init; } = string.Empty;

    [SwaggerSchema("UTC expiry of the access token.")]
    public DateTime ExpiresAt { get; init; }

    [SwaggerSchema("The authenticated customer's profile.")]
    public CustomerDto Customer { get; init; } = default!;
}

[SwaggerSchema("Public customer profile payload.")]
public record CustomerDto
{
    [SwaggerSchema("Customer's ApplicationUser id (the `sub` claim).")]
    public string Id { get; init; } = string.Empty;

    [SwaggerSchema("Email address used to log in.")]
    public string Email { get; init; } = string.Empty;

    [SwaggerSchema("Display name shown in the UI.")]
    public string FullName { get; init; } = string.Empty;

    [SwaggerSchema("Role claim: `Customer` or `Admin`.")]
    public string Role { get; init; } = string.Empty;

    [SwaggerSchema("UTC timestamp of account creation.")]
    public DateTime CreatedAt { get; init; }
}
