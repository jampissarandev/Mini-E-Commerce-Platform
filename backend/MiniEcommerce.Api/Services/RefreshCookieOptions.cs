using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Cookie options builder for the refresh-token cookie. Centralises the
/// Secure-flag policy so the rule (ADR 0005) lives in one place and can be
/// unit-tested without spinning up the integration pipeline.
/// </summary>
public static class RefreshCookieOptions
{
    public const string CookieName = "refresh_token";
    public const string CookiePath = "/api/auth";

    public static CookieOptions Build(DateTime expiresAt, IWebHostEnvironment env) => new()
    {
        HttpOnly = true,
        Secure = ShouldUseSecure(env),
        SameSite = SameSiteMode.Lax,
        Path = CookiePath,
        Expires = expiresAt
    };

    /// <summary>
    /// Secure only in Production. Development, Staging and Testing run over
    /// plain HTTP — a Secure=true cookie is dropped by the browser and
    /// silent refresh fails on every local dev session.
    /// </summary>
    public static bool ShouldUseSecure(IWebHostEnvironment env)
        => env.IsEnvironment("Production");
}
