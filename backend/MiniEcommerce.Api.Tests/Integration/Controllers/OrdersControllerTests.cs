using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>OrdersController</c>. These go through the full
/// ASP.NET Core pipeline using the in-memory database configured by
/// <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class OrdersControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private int[] _productIds = [];

    public OrdersControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
        _productIds = await GetSeededProductIdsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── POST /api/orders ───────────────

    [Fact]
    public async Task Checkout_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "123 Test Street"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_EmptyCart_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-empty-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "123 Test Street"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("EMPTY_CART");
    }

    [Fact]
    public async Task Checkout_InsufficientStock_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-stock-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        // Add 2 items to cart
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });

        // Directly reduce stock to 1 via DB
        using (var context = _factory.CreateDbContext())
        {
            var product = await context.Products.FindAsync(productId);
            product!.Stock = 1;
            await context.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "123 Test Street"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Checkout_InvalidShippingAddress_Returns400ValidationError()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-addr-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add an item so cart is not empty
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 1
        });

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_WithValidCart_Returns201AndCreatesOrder()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-ok-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productA = _productIds[0];
        var productB = _productIds[1];

        // Add two items
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productA,
            Quantity = 2
        });
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productB,
            Quantity = 1
        });

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "456 Main Avenue, Cityville"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Id.Should().BeGreaterThan(0);
        body.Data.Status.Should().Be("Paid");
        body.Data.ShippingAddress.Should().Be("456 Main Avenue, Cityville");
        body.Data.Items.Should().HaveCount(2);
        body.Data.Subtotal.Should().BeGreaterThan(0);
        body.Data.Total.Should().Be(body.Data.Subtotal + body.Data.ShippingFee);
        body.Data.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Checkout_DeductsStockAndClearsCart()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-stock-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        // Get initial stock
        int initialStock;
        using (var context = _factory.CreateDbContext())
        {
            var product = await context.Products.FindAsync(productId);
            initialStock = product!.Stock;
        }

        // Add 3 items
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 3
        });

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "789 Oak Lane"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify stock was deducted
        using (var context = _factory.CreateDbContext())
        {
            var product = await context.Products.FindAsync(productId);
            product!.Stock.Should().Be(initialStock - 3);
        }

        // Verify cart is cleared
        var cart = await client.GetFromJsonAsync<ApiResponse<CartDto>>("/api/cart", Json);
        cart!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Checkout_CapturesCorrectOrderItemDetails()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-details-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });

        var response = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "321 Pine Road"
        });

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json);
        var orderItem = body!.Data!.Items.Should().ContainSingle().Which;
        orderItem.ProductId.Should().Be(productId);
        orderItem.ProductName.Should().NotBeNullOrEmpty();
        orderItem.Quantity.Should().Be(2);
        orderItem.UnitPrice.Should().BeGreaterThan(0);
        orderItem.Subtotal.Should().Be(orderItem.UnitPrice * 2);
    }

    // ─────────────── GET /api/orders ───────────────

    [Fact]
    public async Task GetOrders_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrders_WithNoOrders_Returns200WithEmptyList()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-list-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_ReturnsOnlyCurrentUserOrders()
    {
        var client = _factory.CreateClient();

        // User A places an order
        var tokenA = await RegisterAndLoginAsync(client, "order-list-a@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 1
        });
        await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "User A Address"
        });

        // User B places an order
        var tokenB = await RegisterAndLoginAsync(client, "order-list-b@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[1],
            Quantity = 1
        });
        await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "User B Address"
        });

        // User A should only see their order
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await client.GetAsync("/api/orders");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>(Json);
        body!.Data!.Should().HaveCount(1);
        body.Data![0].ShippingAddress.Should().Be("User A Address");
    }

    [Fact]
    public async Task GetOrders_WithPagination_ReturnsPagedResults()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-page-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Place 3 orders for the same user.
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
            {
                ProductId = _productIds[i],
                Quantity = 1
            });
            await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
            {
                ShippingAddress = $"Page Test Address {i}"
            });
        }

        // page=1, pageSize=2 → 2 items, total=3, totalPages=2
        var response = await client.GetAsync("/api/orders?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Should().HaveCount(2);
        body.Meta.Should().NotBeNull();
        body.Meta!.Page.Should().Be(1);
        body.Meta.PageSize.Should().Be(2);
        body.Meta.TotalCount.Should().Be(3);
        body.Meta.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetOrders_PageSizeRespected_ReturnsRequestedSlice()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-page-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Place 3 orders.
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
            {
                ProductId = _productIds[i],
                Quantity = 1
            });
            await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
            {
                ShippingAddress = $"Size Test {i}"
            });
        }

        // page=2, pageSize=1 → 1 item (the second-newest)
        var response = await client.GetAsync("/api/orders?page=2&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>(Json);
        body!.Data!.Should().HaveCount(1);
        body.Meta!.Page.Should().Be(2);
        body.Meta.PageSize.Should().Be(1);
        body.Meta.TotalCount.Should().Be(3);
        body.Meta.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetOrders_DefaultsToFirstPageWithTenItems()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-page-default@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // No query params → defaults (page=1, pageSize=10) and meta present.
        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderDto>>>(Json);
        body!.Meta.Should().NotBeNull();
        body.Meta!.Page.Should().Be(1);
        body.Meta.PageSize.Should().Be(10);
        body.Meta.TotalCount.Should().Be(0);
        body.Meta.TotalPages.Should().Be(0);
    }

    // ─────────────── GET /api/orders/:id ───────────────

    [Fact]
    public async Task GetOrderById_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/orders/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrderById_NonExistentOrder_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-get-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/orders/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task GetOrderById_AnotherUsersOrder_Returns404()
    {
        var client = _factory.CreateClient();

        // User A places an order
        var tokenA = await RegisterAndLoginAsync(client, "order-get-a@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 1
        });
        var orderResponse = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "User A"
        });
        var orderBody = await orderResponse.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json);
        var orderId = orderBody!.Data!.Id;

        // User B tries to access User A's order
        var tokenB = await RegisterAndLoginAsync(client, "order-get-b@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var response = await client.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrderById_ValidOrder_Returns200WithDetails()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "order-get-ok@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });
        var createResponse = await client.PostAsJsonAsync("/api/orders", new CheckoutRequest
        {
            ShippingAddress = "100 Checkout Lane"
        });
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json))!;

        var response = await client.GetAsync($"/api/orders/{created.Data!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Id.Should().Be(created.Data.Id);
        body.Data.ShippingAddress.Should().Be("100 Checkout Lane");
        body.Data.Items.Should().HaveCount(1);
        body.Data.Items[0].ProductId.Should().Be(productId);
        body.Data.Items[0].Quantity.Should().Be(2);
    }

    // ─────────────── helpers ───────────────

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Order Test User"
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }

    private async Task<int[]> GetSeededProductIdsAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products?pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        return body!.Data!.Select(p => p.Id).ToArray();
    }
}
