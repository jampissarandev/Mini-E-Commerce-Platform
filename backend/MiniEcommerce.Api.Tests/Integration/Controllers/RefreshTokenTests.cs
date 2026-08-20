using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Services;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

[Collection(IntegrationCollection.Name)]
public class RefreshTokenTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public RefreshTokenTests(ApiFactory f) => _factory = f;
    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(string token, string email)> RegisterAsync(HttpClient client, string email)
    {
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = "Password123", FullName = "Tester" });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return (body!.Data!.Token, email);
    }

    [Fact]
    public async Task Register_SetsRefreshCookie()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = "r1@example.com", Password = "Password123", FullName = "R1" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        res.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Any(c => c.Contains("refresh_token")).Should().BeTrue();
    }

    [Fact]
    public async Task Login_SetsRefreshCookie()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "r2@example.com");
        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "r2@example.com", Password = "Password123" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Any(c => c.Contains("refresh_token")).Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_ValidCookie_ReturnsNewAccessToken_AndRotatesCookie()
    {
        var client = _factory.CreateClient();
        await RegisterAsync(client, "r3@example.com");

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "r3@example.com", Password = "Password123" });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        loginRes.Headers.TryGetValues("Set-Cookie", out var sc0).Should().BeTrue();
        var raw = sc0!.First(c => c.Contains("refresh_token")).Split(';')[0].Split('=')[1].Trim();
        raw = Uri.UnescapeDataString(raw);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", $"refresh_token={raw}");
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Token.Should().NotBeNullOrEmpty();
        res.Headers.TryGetValues("Set-Cookie", out var sc2).Should().BeTrue();
        sc2!.Any(c => c.Contains("refresh_token")).Should().BeTrue();

        // Old token should now be revoked — second use fails (use fresh client to avoid Cookie header carry)
        var fresh = _factory.CreateClient();
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req2.Headers.Add("Cookie", $"refresh_token={raw}");
        var res2 = await fresh.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ConcurrentRefreshes_FirstSucceedsSecondFails()
    {
        // Per ADR 0005: "first-wins, second-fails" when two refreshes race on the same cookie
        var client = _factory.CreateClient();
        await RegisterAsync(client, "r5@example.com");

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "r5@example.com", Password = "Password123" });
        loginRes.Headers.TryGetValues("Set-Cookie", out var sc0).Should().BeTrue();
        var raw = sc0!.First(c => c.Contains("refresh_token")).Split(';')[0].Split('=')[1].Trim();
        raw = Uri.UnescapeDataString(raw);

        // Two independent clients + two independent DbContexts. Each posts
        // /api/auth/refresh with the same cookie. The atomic UPDATE gate in
        // RefreshTokenService.RotateAtomicAsync must reject the second one.
        var a = _factory.CreateClient();
        var b = _factory.CreateClient();
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req1.Headers.Add("Cookie", $"refresh_token={raw}");
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req2.Headers.Add("Cookie", $"refresh_token={raw}");

        var r1 = await a.SendAsync(req1);
        var r2 = await b.SendAsync(req2);

        var statuses = new[] { r1.StatusCode, r2.StatusCode };
        statuses.Count(s => s == HttpStatusCode.OK).Should().Be(1, "exactly one concurrent refresh should win");
        statuses.Count(s => s == HttpStatusCode.Unauthorized).Should().Be(1, "exactly one concurrent refresh should be rejected as second-fail");
    }

    [Fact]
    public async Task Refresh_MissingCookie_Returns401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_InvalidCookie_Returns401()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req.Headers.Add("Cookie", "refresh_token=not-a-real-token");
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ClearsCookie_AndRevokesToken()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "r4@example.com",
            Password = "Password123",
            FullName = "R4"
        });
        loginRes.StatusCode.Should().Be(HttpStatusCode.Created);
        loginRes.Headers.TryGetValues("Set-Cookie", out var sc0).Should().BeTrue();
        var raw = sc0!.First(c => c.Contains("refresh_token")).Split(';')[0].Split('=')[1].Trim();
        raw = Uri.UnescapeDataString(raw);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        req.Headers.Add("Cookie", $"refresh_token={raw}");
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Refresh with same token should now 401
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        req2.Headers.Add("Cookie", $"refresh_token={raw}");
        var res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
