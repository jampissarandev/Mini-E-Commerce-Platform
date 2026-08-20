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

    public async Task<RefreshToken?> FindByRawAsync(string raw, CancellationToken ct = default)
    {
        var hash = HashToken(raw);
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    }

    /// <summary>
    /// Race-safe rotation per ADR 0005: "first-wins, second-fails".
    /// 1. Issue the replacement token first (so it has an Id).
    /// 2. Attempt an atomic conditional UPDATE on the *current* token: only
    ///    set RevokedAt if it is still null AND not expired. Two concurrent
    ///    refreshes with the same current token both issue a replacement,
    ///    but only the first UPDATE succeeds (rowsAffected==1). The loser
    ///    gets rowsAffected==0 and we revoke the loser's replacement.
    ///
    /// The InMemory provider (used by tests) does not support raw SQL, so
    /// we fall back to a tracked-entity conditional update that has the same
    /// first-wins semantics within a single DbContext. Multi-DbContext races
    /// in tests are not supported; the InMemory guarantee is sufficient for
    /// single-process tests. The Postgres path is what real production traffic
    /// hits, and that one is genuinely atomic.
    /// </summary>
    public async Task<(RefreshToken newToken, string newRaw)?> RotateAtomicAsync(RefreshToken current, CancellationToken ct = default)
    {
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

        // Issue the new token (always succeeds, returns new Id)
        var (next, raw) = await IssueAsync(current.CustomerId, ct);

        bool claimed;
        if (isInMemory)
        {
            // Single-DbContext test path: gate by reading a fresh snapshot.
            _context.Entry(current).State = EntityState.Detached;
            var fresh = await _context.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == current.Id, ct);
            claimed = fresh is not null && fresh.RevokedAt is null && fresh.ExpiresAt > DateTime.UtcNow;
            if (claimed)
            {
                current.RevokedAt = DateTime.UtcNow;
                current.ReplacedById = next.Id;
                _context.RefreshTokens.Attach(current);
                _context.Entry(current).State = EntityState.Modified;
                await _context.SaveChangesAsync(ct);
            }
        }
        else
        {
            var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"RefreshTokens\" SET \"RevokedAt\" = {DateTime.UtcNow}, \"ReplacedById\" = {next.Id} WHERE \"Id\" = {current.Id} AND \"RevokedAt\" IS NULL AND \"ExpiresAt\" > {DateTime.UtcNow}",
                ct);
            claimed = rowsAffected == 1;
        }

        if (!claimed)
        {
            // Lost the race. Revoke the replacement we just issued.
            next.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return null;
        }

        return (next, raw);
    }

    public async Task RevokeAsync(RefreshToken token, CancellationToken ct = default)
    {
        if (token.RevokedAt is not null) return;
        var isInMemory = _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
        if (isInMemory)
        {
            // Detach any tracked copy, then re-fetch by Id from the InMemory store
            // so the SaveChanges update lands on the live row, not a stale tracked
            // snapshot from a different DbContext.
            var tracked = _context.ChangeTracker.Entries<RefreshToken>()
                .FirstOrDefault(e => e.Entity.Id == token.Id);
            if (tracked is not null) tracked.State = EntityState.Detached;

            var fresh = await _context.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == token.Id, ct);
            if (fresh is null || fresh.RevokedAt is not null) return;
            fresh.RevokedAt = DateTime.UtcNow;
            _context.RefreshTokens.Attach(fresh);
            _context.Entry(fresh).State = EntityState.Modified;
            await _context.SaveChangesAsync(ct);
            return;
        }
        token.RevokedAt = DateTime.UtcNow;
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"RefreshTokens\" SET \"RevokedAt\" = {DateTime.UtcNow} WHERE \"Id\" = {token.Id} AND \"RevokedAt\" IS NULL",
            ct);
    }
}
