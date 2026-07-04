using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AuthController</c>. These go through the full
/// ASP.NET Core pipeline (routing, model binding, Identity, JWT, JsonOptions)
/// using the in-memory database configured by <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AuthControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuthControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── /auth/register ───────────────

    [Fact]
    public async Task Register_WithValidInput_Returns201WithJwt()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Email = "alice@example.com",
            Password = "Password123",
            FullName = "Alice Wonderland"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.Token.Should().NotBeNullOrEmpty();
        body.Data.User.Email.Should().Be("alice@example.com");
        body.Data.User.Role.Should().Be("Customer");
        body.Data.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Email = "dup@example.com",
            Password = "Password123",
            FullName = "Dup One"
        };
        await client.PostAsJsonAsync("/api/auth/register", request);

        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await second.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("REGISTRATION_FAILED");
    }

    [Fact]
    public async Task Register_WithMissingFields_Returns400ValidationError()
    {
        var client = _factory.CreateClient();
        // Email missing, password too short
        var request = new RegisterRequest
        {
            Email = "",
            Password = "abc",
            FullName = ""
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("VALIDATION_ERROR");
        body.Error.Details.Should().NotBeNull();
    }

    // ─────────────── /auth/login ───────────────

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200WithJwt()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "bob@example.com",
            Password = "Password123",
            FullName = "Bob Builder"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "bob@example.com",
            Password = "Password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        body!.Data!.Token.Should().NotBeNullOrEmpty();
        body.Data.User.Email.Should().Be("bob@example.com");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "carol@example.com",
            Password = "Password123",
            FullName = "Carol Danvers"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "carol@example.com",
            Password = "WRONG-PASSWORD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "Password123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────── JWT shape ───────────────

    [Fact]
    public async Task Login_TokenContainsRoleClaim_AndSubMatchesUserId()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "dave@example.com",
            Password = "Password123",
            FullName = "Dave Lister"
        });

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "dave@example.com",
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        var token = body!.Data!.Token;

        // Decode the JWT without validating the signature — we trust the issuer here.
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear(); // don't remap short claim names
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Customer");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "dave@example.com");
    }

    // ─────────────── /auth/me ───────────────

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "eve@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(Json);
        body!.Data!.Email.Should().Be("eve@example.com");
        body.Data.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-jwt");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────── helpers ───────────────

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Test User"
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }
}
