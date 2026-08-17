using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Controllers;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AdminDashboardController</c> (Task 17a).
/// Tests cover: role gating on every endpoint, summary numbers matching
/// direct DB queries, the daily sales series (exact day count + totals),
/// recent-orders ordering/limit, and low-stock threshold filtering.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AdminDashboardControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminDashboardControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/summary — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSummary_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSummary_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-summary@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/summary — Numbers match DB
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSummary_WithAdminToken_ReturnsNumbersMatchingDb()
    {
        var client = _factory.CreateClient();

        // Register a customer and place an order so the summary is non-trivial.
        var userToken = await RegisterAndLoginAsync(client, "summary-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client);

        // Admin requests the summary.
        var adminToken = await CreateAdminAndLoginAsync(client, "summary-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardSummary>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();

        // Every number must match a direct DB query.
        using var ctx = _factory.CreateDbContext();
        var expectedOrders = await ctx.Orders.CountAsync();
        var expectedRevenue = await ctx.Orders.SumAsync(o => (decimal?)o.Total) ?? 0m;
        var customerRoleId = await ctx.Roles
            .Where(r => r.Name == "Customer")
            .Select(r => r.Id)
            .FirstAsync();
        var expectedCustomers = await ctx.UserRoles.CountAsync(ur => ur.RoleId == customerRoleId);
        var expectedProducts = await ctx.Products.CountAsync();
        var expectedLowStock = await ctx.Products
            .CountAsync(p => p.Stock <= AdminDashboardController.DefaultLowStockThreshold);

        body.Data!.TotalOrders.Should().Be(expectedOrders);
        body.Data.TotalRevenue.Should().Be(expectedRevenue);
        body.Data.TotalCustomers.Should().Be(expectedCustomers);
        body.Data.TotalProducts.Should().Be(expectedProducts);
        body.Data.LowStockCount.Should().Be(expectedLowStock);
    }

    [Fact]
    public async Task GetSummary_TotalCustomers_CountsCustomerRoleOnly()
    {
        var client = _factory.CreateClient();

        // Register a customer (Customer role) and an admin (Admin role).
        await RegisterAndLoginAsync(client, "role-customer@example.com");
        await CreateAdminAndLoginAsync(client, "role-admin@example.com");

        var adminToken = await CreateAdminAndLoginAsync(client, "role-admin-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/summary");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardSummary>>(Json);
        // Only the Customer-role user counts — admins are not customers.
        body!.Data!.TotalCustomers.Should().Be(1);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/sales — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSales_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/dashboard/sales");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSales_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-sales@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard/sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/sales — Series shape
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetSales_ReturnsExactlyRequestedDayCount()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "sales-count-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/dashboard/sales?days=30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesPoint>>>(Json);
        body!.Data.Should().HaveCount(30);
        // Dates are contiguous, ascending, ISO-8601.
        body.Data!.Select(p => p.Date).Should().OnlyHaveUniqueItems();
        body.Data.Should().BeInAscendingOrder(p => p.Date);
        body.Data![0].Date.Should().Match("????-??-??");
    }

    [Fact]
    public async Task GetSales_TotalsAndOrderCounts_MatchDb()
    {
        var client = _factory.CreateClient();

        // Insert orders on controlled dates: today, 3 days ago, 29 days ago.
        var today = DateTime.UtcNow.Date;
        await InsertOrderAsync("sales-today@example.com", total: 100m, createdAt: today.AddHours(12));
        await InsertOrderAsync("sales-3d@example.com", total: 50m, createdAt: today.AddDays(-3).AddHours(12));
        await InsertOrderAsync("sales-29d@example.com", total: 25m, createdAt: today.AddDays(-29).AddHours(12));

        var adminToken = await CreateAdminAndLoginAsync(client, "sales-totals-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/sales?days=30");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesPoint>>>(Json);
        body!.Data.Should().HaveCount(30);

        var byDate = body.Data!.ToDictionary(p => p.Date, p => p);

        byDate[today.ToString("yyyy-MM-dd")].Total.Should().Be(100m);
        byDate[today.ToString("yyyy-MM-dd")].OrderCount.Should().Be(1);
        byDate[today.AddDays(-3).ToString("yyyy-MM-dd")].Total.Should().Be(50m);
        byDate[today.AddDays(-29).ToString("yyyy-MM-dd")].Total.Should().Be(25m);

        // A day with no orders is zero-filled.
        byDate[today.AddDays(-1).ToString("yyyy-MM-dd")].Total.Should().Be(0m);
        byDate[today.AddDays(-1).ToString("yyyy-MM-dd")].OrderCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSales_OrdersOutsideWindow_AreExcluded()
    {
        var client = _factory.CreateClient();

        // Insert an order 31 days ago — outside a 30-day window.
        var today = DateTime.UtcNow.Date;
        await InsertOrderAsync("sales-old@example.com", total: 999m, createdAt: today.AddDays(-31).AddHours(12));

        var adminToken = await CreateAdminAndLoginAsync(client, "sales-window-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/sales?days=30");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<SalesPoint>>>(Json);
        body!.Data.Should().HaveCount(30);
        body.Data!.Sum(p => p.Total).Should().Be(0m);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/recent-orders — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRecentOrders_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/dashboard/recent-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRecentOrders_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-recent@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard/recent-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/recent-orders — Ordering + limit
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRecentOrders_ReturnsNewestFirst_RespectsLimit()
    {
        var client = _factory.CreateClient();

        // Insert 3 orders with controlled, distinct CreatedAt values.
        var today = DateTime.UtcNow.Date;
        await InsertOrderAsync("recent-oldest@example.com", total: 10m, createdAt: today.AddDays(-2).AddHours(12));
        await InsertOrderAsync("recent-middle@example.com", total: 20m, createdAt: today.AddDays(-1).AddHours(12));
        await InsertOrderAsync("recent-newest@example.com", total: 30m, createdAt: today.AddHours(12));

        var adminToken = await CreateAdminAndLoginAsync(client, "recent-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/recent-orders?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data.Should().HaveCount(2);
        body.Data![0].Total.Should().Be(30m);
        body.Data[1].Total.Should().Be(20m);
        body.Data.Should().BeInDescendingOrder(o => o.CreatedAt);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/low-stock — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLowStock_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/dashboard/low-stock");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLowStock_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-lowstock@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/dashboard/low-stock");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/dashboard/low-stock — Threshold filtering
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetLowStock_ReturnsProductsAtOrBelowThreshold()
    {
        var client = _factory.CreateClient();

        // Set three products to 5 / 10 / 11 stock.
        using (var ctx = _factory.CreateDbContext())
        {
            var products = await ctx.Products.OrderBy(p => p.Id).Take(3).ToListAsync();
            products[0].Stock = 5;
            products[1].Stock = 10;
            products[2].Stock = 11;
            await ctx.SaveChangesAsync();
        }

        var adminToken = await CreateAdminAndLoginAsync(client, "lowstock-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/low-stock?threshold=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<LowStockProductDto>>>(Json);
        body!.Data.Should().HaveCount(2);
        body.Data!.Should().OnlyContain(p => p.Stock <= 10);
        // Ordered by stock ascending (most urgent first).
        body.Data![0].Stock.Should().Be(5);
        body.Data[1].Stock.Should().Be(10);
    }

    [Fact]
    public async Task GetLowStock_ThresholdZero_ReturnsOnlyOutOfStock()
    {
        var client = _factory.CreateClient();

        using (var ctx = _factory.CreateDbContext())
        {
            var products = await ctx.Products.OrderBy(p => p.Id).Take(2).ToListAsync();
            products[0].Stock = 0;
            products[1].Stock = 5;
            await ctx.SaveChangesAsync();
        }

        var adminToken = await CreateAdminAndLoginAsync(client, "lowstock-zero-admin@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/dashboard/low-stock?threshold=0");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<LowStockProductDto>>>(Json);
        body!.Data.Should().ContainSingle();
        body.Data![0].Stock.Should().Be(0);
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

    /// <summary>
    /// Registers a fresh Customer, logs them in, and returns the JWT.
    /// </summary>
    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email, string fullName = "Test User")
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = fullName
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }

    /// <summary>
    /// Registers a user, promotes them to Admin via the shared
    /// <see cref="DbContextExtensions.SeedAdminAsync"/> host extension, then
    /// logs them in and returns the Admin JWT.
    /// </summary>
    private async Task<string> CreateAdminAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Admin User"
        });

        await _factory.SeedAdminAsync(email);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }

    /// <summary>
    /// Inserts an Order + OrderItem directly in the database with a controlled
    /// <paramref name="createdAt"/> and <paramref name="total"/>, so the sales
    /// series and recent-orders tests can pin exact dates. Registers
    /// <paramref name="email"/> as a customer first so the FK is satisfied.
    /// </summary>
    private async Task<int> InsertOrderAsync(string email, decimal total, DateTime createdAt)
    {
        var registerClient = _factory.CreateClient();
        await registerClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Dashboard Customer"
        });

        using var ctx = _factory.CreateDbContext();
        var user = await ctx.Users.FirstAsync(u => u.Email == email);
        var product = await ctx.Products.OrderBy(p => p.Id).FirstAsync();

        var order = new Order
        {
            CustomerId = user.Id,
            Status = OrderStatus.Paid,
            Subtotal = total,
            ShippingFee = 0m,
            Total = total,
            ShippingFullName = "Dashboard Customer",
            ShippingStreet = "123 Test St",
            ShippingCity = "Testville",
            ShippingPostalCode = "12345",
            ShippingCountry = "USA",
            ShippingPhone = "+1-555-0100",
            CreatedAt = createdAt,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = total,
            Quantity = 1,
        });
        await ctx.SaveChangesAsync();

        return order.Id;
    }
}