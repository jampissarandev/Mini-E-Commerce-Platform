using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// Role-based access control tests for <c>AdminController</c>. Verifies that
/// the Auth checkpoint requirements are met:
///   - Unauthenticated → 401
///   - Customer role     → 403
///   - Admin role        → 200
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AdminControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── /api/admin/dashboard ───────────────

    [Fact]
    public async Task Dashboard_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "customer@test.com", "Customer");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Dashboard_WithAdminToken_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "admin@test.com", "Admin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(Json);
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    // ─────────────── helpers ───────────────

    /// <summary>
    /// Creates a user with the specified role via UserManager directly
    /// (matching the Seed pattern) then logs in via HTTP to get a JWT.
    /// </summary>
    private async Task<string> RegisterAndLoginAsync(
        HttpClient client, string email, string role)
    {
        // Create the user directly in the database with the correct role
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Test User",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, "Password123");
            result.Succeeded.Should().BeTrue();

            await userManager.AddToRoleAsync(user, role);
        }

        // Login via HTTP to get a JWT with the correct role claim
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }
}
