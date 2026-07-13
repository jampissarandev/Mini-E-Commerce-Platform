using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AdminOrdersController</c> (ticket 15a).
/// Tests cover: role gating, cross-customer visibility, status filtering,
/// date-range filtering, free-text search, pagination meta, and ordering.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AdminOrdersControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminOrdersControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminOrders_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-admin-orders@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Invalid status filter
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_InvalidStatus_Returns400()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-invalid-status@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/orders?status=Banana");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("INVALID_STATUS");
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Returns all orders across customers
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_WithAdminToken_ReturnsAllOrdersAcrossCustomers()
    {
        var client = _factory.CreateClient();

        // Customer A places an order
        var tokenA = await RegisterAndLoginAsync(client, "admin-list-a@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await AddToCartAndCheckout(client, fullName: "Customer A");

        // Customer B places an order
        var tokenB = await RegisterAndLoginAsync(client, "admin-list-b@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await AddToCartAndCheckout(client, fullName: "Customer B");

        // Admin sees both orders
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-list-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Status filter
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_FilterByStatus_ReturnsOnlyMatchingOrders()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-status-filter@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order (will be Paid)
        var userToken = await RegisterAndLoginAsync(client, "status-filter-user@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client);

        // Filter by Paid status
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?status=Paid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().OnlyContain(o => o.Status == "Paid");
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Date range filter
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_FilterByDateRange_NarrowsResults()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-date-filter@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order
        var userToken = await RegisterAndLoginAsync(client, "date-filter-user@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client);

        // Filter to today only — should find the order
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync($"/api/admin/orders?from={today}&to={today}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();

        // Filter to a date far in the past — should find nothing
        var response2 = await client.GetAsync("/api/admin/orders?from=2000-01-01&to=2000-01-01");
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body2!.Data!.Should().BeEmpty();
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Free-text search (email)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_SearchByEmail_FindsCustomerOrder()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-email-search@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order
        var userToken = await RegisterAndLoginAsync(client, "email-search-user@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client);

        // Search by partial email
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?q=email-search-user");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().OnlyContain(o =>
            o.CustomerEmail.Contains("email-search-user", StringComparison.OrdinalIgnoreCase));
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Free-text search (order id)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_SearchByOrderId_FindsOrder()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-id-search@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order and capture its ID
        var userToken = await RegisterAndLoginAsync(client, "id-search-user@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);

        // Search by order ID
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync($"/api/admin/orders?q={orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().ContainSingle();
        body.Data![0].Id.Should().Be(orderId);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Pagination meta
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_Pagination_MetaIsCorrect()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-pagination@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place 3 orders
        for (var i = 0; i < 3; i++)
        {
            var userToken = await RegisterAndLoginAsync(client, $"page-user-{i}@example.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            await AddToCartAndCheckout(client);
        }

        // Request page 1, pageSize 2
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().HaveCount(2);
        body.Meta.Should().NotBeNull();
        body.Meta!.Page.Should().Be(1);
        body.Meta.PageSize.Should().Be(2);
        body.Meta.TotalCount.Should().BeGreaterThanOrEqualTo(3);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders — Newest first ordering
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_OrderedNewestFirst()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-ordering@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place 3 orders sequentially
        for (var i = 0; i < 3; i++)
        {
            var userToken = await RegisterAndLoginAsync(client, $"order-user-{i}@example.com");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            await AddToCartAndCheckout(client);
        }

        // Fetch all — should be newest first
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?pageSize=100");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().HaveCountGreaterThanOrEqualTo(3);

        // The first item's CreatedAt should be >= the last item's CreatedAt
        var timestamps = body.Data!.Select(o => o.CreatedAt).ToList();
        timestamps.Should().BeInDescendingOrder();
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Adds the first seeded product to the cart and checks out.
    /// Returns the newly created order ID.
    /// </summary>
    private async Task<int> AddToCartAndCheckout(HttpClient client, string fullName = "Test Customer")
    {
        // Get a product ID from the seeded catalog
        int productId;
        using (var ctx = _factory.CreateDbContext())
        {
            productId = await ctx.Products.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        }

        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 1
        });

        var checkoutResponse = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            FullName = fullName,
            Street = "123 Test Street",
            City = "Testville",
            PostalCode = "12345",
            Country = "USA",
            Phone = "+1-555-0100"
        });

        var body = await checkoutResponse.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json);
        return body!.Data!.Id;
    }

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

    private async Task<string> CreateAdminAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Admin User"
        });

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }
}
