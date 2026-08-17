using System.ComponentModel.DataAnnotations;

namespace MiniEcommerce.Api.Dtos;

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; init; } = string.Empty;
}

public record LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public CustomerDto Customer { get; init; } = default!;
}

public record CustomerDto
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
