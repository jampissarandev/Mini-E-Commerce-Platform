using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Services;

public class RefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public RefreshTokenService(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public int RefreshExpiresInDays => _config.GetValue<int>("Jwt:RefreshExpiresInDays", 30);

    /// <summary>
    /// Issues a refresh token and returns (entity, rawToken). The raw token is only returned once (for the cookie).
    /// </summary>
    public async Task<(RefreshToken entity, string raw)> IssueAsync(string customerId, CancellationToken ct = default)
    {
        var raw = GenerateRawToken();
        var entity = new RefreshToken
        {
            CustomerId = customerId,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshExpiresInDays),
        };
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync(ct);
        return (entity, raw);
    }

    public async Task<(RefreshToken entity, string raw)> IssueWithRawAsync(string customerId, CancellationToken ct = default)
        => await IssueAsync(customerId, ct);

    public async Task<RefreshToken?> FindByRawAsync(string raw, CancellationToken ct = default)
    {
        var hash = HashToken(raw);
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    }

    public async Task<(RefreshToken newToken, string newRaw)?> RotateAsync(RefreshToken current, CancellationToken ct = default)
    {
        if (current.RevokedAt is not null) return null;
        if (current.ExpiresAt < DateTime.UtcNow) return null;

        var (next, raw) = await IssueAsync(current.CustomerId, ct);
        current.RevokedAt = DateTime.UtcNow;
        current.ReplacedById = next.Id;
        await _context.SaveChangesAsync(ct);
        return (next, raw);
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken ct = default)
    {
        token.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
    }
}
