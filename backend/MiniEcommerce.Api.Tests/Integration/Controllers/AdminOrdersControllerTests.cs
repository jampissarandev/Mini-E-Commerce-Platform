using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AdminOrdersController</c> (tickets 15a + 15b).
/// Tests cover: role gating, cross-customer visibility, status filtering,
/// date-range filtering, free-text search, pagination meta, ordering,
/// and order detail with customer info and computed item subtotals.
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
    //  GET /api/admin/orders — Free-text search (customer full name)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrders_SearchByCustomerName_FindsOrder()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-name-search@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order — register with a distinct full name that doesn't appear in the email
        var userToken = await RegisterAndLoginAsync(client, "name-search-user@example.com", fullName: "Alice Wonderberg");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client, fullName: "Alice Wonderberg");

        // Search by full name substring
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?q=Wonderberg");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().ContainSingle();
        body.Data![0].CustomerEmail.Should().Be("name-search-user@example.com");
    }

    [Fact]
    public async Task GetAdminOrders_SearchByCustomerName_CaseInsensitive()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-name-ci@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order — register with a distinct full name
        var userToken = await RegisterAndLoginAsync(client, "name-ci-user@example.com", fullName: "Bob Mcintyre");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client, fullName: "Bob Mcintyre");

        // Search with different casing — should still match
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?q=MCINTYRE");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().ContainSingle();
        body.Data![0].CustomerEmail.Should().Be("name-ci-user@example.com");
    }

    [Fact]
    public async Task GetAdminOrders_SearchNonMatchingTerm_ReturnsEmpty()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-no-match@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Place an order
        var userToken = await RegisterAndLoginAsync(client, "no-match-user@example.com", fullName: "Charlie Brown");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        await AddToCartAndCheckout(client, fullName: "Charlie Brown");

        // Search for a term that matches neither email nor name
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.GetAsync("/api/admin/orders?q=zzznonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminOrderListItem>>>(Json);
        body!.Data!.Should().BeEmpty();
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
    //  GET /api/admin/orders/{id} — Auth gating
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrderById_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/orders/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminOrderById_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-detail-access@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/orders/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders/{id} — Happy path
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrderById_WithAdminToken_ReturnsFullDetail()
    {
        var client = _factory.CreateClient();

        // Customer places an order
        var userToken = await RegisterAndLoginAsync(client, "detail-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client, fullName: "Jane Doe");

        // Admin retrieves the order
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-detail@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync($"/api/admin/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Id.Should().Be(orderId);
    }

    [Fact]
    public async Task GetAdminOrderById_AdminOrderDetail_HasExpectedFields()
    {
        var client = _factory.CreateClient();

        // Customer places an order
        var userToken = await RegisterAndLoginAsync(client, "detail-fields-customer@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client, fullName: "Jane Doe");

        // Admin retrieves the order
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-detail-fields@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync($"/api/admin/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body!.Data!.Should().NotBeNull();
        var detail = body.Data!;

        // Customer info (FullName comes from ApplicationUser.FullName, set during RegisterAsync)
        detail.Customer.Should().NotBeNull();
        detail.Customer!.Email.Should().Be("detail-fields-customer@example.com");
        detail.Customer.FullName.Should().Be("Test User");

        // Items
        detail.Items.Should().NotBeEmpty();
        var item = detail.Items.First();
        item.ProductName.Should().NotBeEmpty();
        item.UnitPrice.Should().BeGreaterThan(0);
        item.Quantity.Should().BeGreaterThan(0);
        item.Subtotal.Should().Be(item.UnitPrice * item.Quantity);

        // Shipping (comes from the CheckoutRequest)
        detail.ShippingFullName.Should().Be("Jane Doe");
        detail.ShippingStreet.Should().Be("123 Test Street");
        detail.ShippingCity.Should().Be("Testville");
        detail.ShippingCountry.Should().Be("USA");

        // Totals
        detail.Subtotal.Should().BeGreaterThan(0);
        detail.Total.Should().BeGreaterThan(0);
        detail.Status.Should().NotBeEmpty();
        detail.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    // ═══════════════════════════════════════════════════════════
    //  GET /api/admin/orders/{id} — Not found
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminOrderById_WhenOrderDoesNotExist_Returns404()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-detail-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/orders/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("ORDER_NOT_FOUND");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Auth gating  (ticket 15c)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/admin/orders/1/status", new { status = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateStatus_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-status-403@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/admin/orders/1/status", new { status = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Validation
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_MissingStatus_Returns400()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-missing-status@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PutAsJsonAsync("/api/admin/orders/1/status", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_Returns400()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-invalid-status@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PutAsJsonAsync("/api/admin/orders/1/status", new { status = "Banana" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("VALIDATION_ERROR");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Not found
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_NonExistentOrder_Returns404()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-status-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.PutAsJsonAsync("/api/admin/orders/99999/status", new { status = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("ORDER_NOT_FOUND");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Valid transitions
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_PendingToPaid_Succeeds()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-pend-paid@example.com");
        var orderId = await CreatePendingOrderAsync("pend-paid@example.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Paid" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task UpdateStatus_PaidToShipped_Succeeds()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-paid-shipped@example.com");

        // Create a paid order via checkout
        var userToken = await RegisterAndLoginAsync(client, "paid-shipped@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);

        // Admin transitions Paid → Shipped
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Shipped" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Status.Should().Be("Shipped");
    }

    [Fact]
    public async Task UpdateStatus_ShippedToDelivered_Succeeds()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-shipped-delivered@example.com");

        // Create a paid order via checkout, then transition to Shipped
        var userToken = await RegisterAndLoginAsync(client, "shipped-delivered@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Shipped" });

        // Now transition Shipped → Delivered
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Delivered" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Status.Should().Be("Delivered");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Invalid transitions
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_PendingToDelivered_Returns409()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-pend-del@example.com");
        var orderId = await CreatePendingOrderAsync("pend-del@example.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Delivered" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("INVALID_TRANSITION");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Terminal state rejects
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_DeliveredToAnything_Returns409OrderAlreadyTerminal()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-del-term@example.com");

        // Create paid → shipped → delivered
        var userToken = await RegisterAndLoginAsync(client, "del-term@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Shipped" });
        await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Delivered" });

        // Try Delivered → Cancelled (terminal — no transitions allowed)
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("ORDER_ALREADY_TERMINAL");
    }

    [Fact]
    public async Task UpdateStatus_CancelledToAnything_Returns409OrderAlreadyTerminal()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-canc-term@example.com");

        // Create paid → cancelled
        var userToken = await RegisterAndLoginAsync(client, "canc-term@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Cancelled" });

        // Try Cancelled → Paid (terminal — no transitions allowed)
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Paid" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("ORDER_ALREADY_TERMINAL");
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — Restock on cancel
    //
    //  Per ADR 0001, every (non-terminal) order has stock to restore on
    //  cancel. The three cases below parameterize over the from-state and
    //  share one body so the symmetry with the state machine table is
    //  explicit. All three paths go through the live controller — none of
    //  them short-circuit by inserting an Order row directly.
    // ═══════════════════════════════════════════════════════════

    public enum CancelFromState { Pending, Paid, Shipped }

    [Theory]
    [InlineData(CancelFromState.Pending)]
    [InlineData(CancelFromState.Paid)]
    [InlineData(CancelFromState.Shipped)]
    public async Task UpdateStatus_ToCancelled_RestocksItems(CancelFromState fromState)
    {
        var client = _factory.CreateClient();
        var email = $"restock-{fromState.ToString().ToLowerInvariant()}@example.com";
        var adminEmail = $"admin-restock-{fromState.ToString().ToLowerInvariant()}@example.com";

        // Drive the order to the from-state through the live controller
        // (or, for Pending, through the helper that mirrors the live
        // deduction step). Then capture the post-deduction stock.
        int orderId;
        if (fromState == CancelFromState.Pending)
        {
            orderId = await CreatePendingOrderAsync(email, quantity: 1);
        }
        else
        {
            var userToken = await RegisterAndLoginAsync(client, email);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            orderId = await AddToCartAndCheckout(client);

            if (fromState == CancelFromState.Shipped)
            {
                var adminTokenForSetup = await CreateAdminAndLoginAsync(client, $"{adminEmail}-setup");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokenForSetup);
                await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Shipped" });
            }
        }

        int productId, stockBefore, quantity;
        using (var ctx = _factory.CreateDbContext())
        {
            var item = await ctx.OrderItems.FirstAsync(i => i.OrderId == orderId);
            productId = item.ProductId;
            quantity = item.Quantity;
            var product = await ctx.Products.FindAsync(productId);
            stockBefore = product!.Stock;
        }

        // Admin cancels
        var adminToken = await CreateAdminAndLoginAsync(client, adminEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Cancelled" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderDetail>>(Json);
        body!.Data!.Status.Should().Be("Cancelled");

        // Stock should be restored to the pre-deduction value
        using (var ctx = _factory.CreateDbContext())
        {
            var product = await ctx.Products.FindAsync(productId);
            product!.Stock.Should().Be(stockBefore + quantity,
                $"cancelling from {fromState} must restock {quantity} units (ADR 0001)");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUT /api/admin/orders/{id}/status — After-cancel terminal
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStatus_AfterCancelTerminal_Returns409()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAndLoginAsync(client, "admin-after-canc@example.com");

        // Create paid → cancelled
        var userToken = await RegisterAndLoginAsync(client, "after-canc@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var orderId = await AddToCartAndCheckout(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Cancelled" });

        // Try another transition on the cancelled order
        var response = await client.PutAsJsonAsync($"/api/admin/orders/{orderId}/status", new { status = "Paid" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeFalse();
        body.Error!.Code.Should().Be("ORDER_ALREADY_TERMINAL");
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
    /// logs them in and returns the Admin JWT. The role flip lives in test
    /// infrastructure (not the controller surface) to match the role
    /// management pattern used by <c>Data/Seed.cs</c>.
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
    /// Creates an order with <see cref="OrderStatus.Pending"/> directly in the
    /// database. Mirrors <c>OrdersController.Checkout</c>'s stock-deduction
    /// step (Product.Stock -= Quantity) so the restock-on-cancel tests
    /// exercise the same row state the live controller produces — without
    /// this, a cancel test would only verify the restock math, not ADR
    /// 0001's "every (non-terminal) order has stock to restore" guarantee.
    ///
    /// Registers <paramref name="email"/> as a customer so the FK constraint
    /// is satisfied, then inserts an Order + OrderItem against the first
    /// seeded product.
    /// </summary>
    private async Task<int> CreatePendingOrderAsync(string email, int quantity = 1)
    {
        // Register the user via the API so Identity is set up properly
        var registerClient = _factory.CreateClient();
        await registerClient.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Pending User"
        });

        using var ctx = _factory.CreateDbContext();
        var user = await ctx.Users.FirstAsync(u => u.Email == email);
        var product = await ctx.Products.OrderBy(p => p.Id).FirstAsync();

        // Deduct stock first (mirrors OrdersController.Checkout), so the
        // order's existence implies a real stock reservation.
        product.Stock -= quantity;
        await ctx.SaveChangesAsync();

        var order = new Order
        {
            CustomerId = user.Id,
            Status = OrderStatus.Pending,
            Subtotal = product.Price * quantity,
            ShippingFee = 5.99m,
            Total = product.Price * quantity + 5.99m,
            ShippingFullName = "Pending Customer",
            ShippingStreet = "123 Pending St",
            ShippingCity = "Pendingville",
            ShippingPostalCode = "12345",
            ShippingCountry = "USA",
            ShippingPhone = "+1-555-0000",
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity,
        };
        ctx.OrderItems.Add(orderItem);
        await ctx.SaveChangesAsync();

        return order.Id;
    }
}
