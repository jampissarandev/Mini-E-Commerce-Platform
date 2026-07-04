using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>CartController</c>. These go through the full
/// ASP.NET Core pipeline using the in-memory database configured by
/// <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class CartControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Product IDs from the seed data. We look these up dynamically because
    // the InMemory provider's auto-increment counter may not reset to 1
    // across test class boundaries.
    private int[] _productIds = [];

    public CartControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
        _productIds = await GetSeededProductIdsAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── GET /api/cart ───────────────

    [Fact]
    public async Task GetCart_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCart_WithToken_EmptyCart_Returns200WithEmptyItems()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-user-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartDto>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Items.Should().BeEmpty();
        body.Data.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCart_WithToken_WithData_ReturnsCartWithItems()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-user-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        // Add an item first
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartDto>>(Json);
        body!.Data!.Items.Should().HaveCount(1);
        body.Data.Items[0].ProductId.Should().Be(productId);
        body.Data.Items[0].Quantity.Should().Be(2);
    }

    // ─────────────── POST /api/cart/items ───────────────

    [Fact]
    public async Task AddCartItem_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = 1,
            Quantity = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddCartItem_WithValidProduct_Returns201()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-add-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.ProductId.Should().Be(productId);
        body.Data.Quantity.Should().Be(3);
        body.Data.UnitPrice.Should().BeGreaterThan(0);
        body.Data.ProductName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AddCartItem_SameProductTwice_IncreasesQuantity()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-add-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        body!.Data!.Quantity.Should().Be(5);

        // Verify via GET
        var cart = await client.GetFromJsonAsync<ApiResponse<CartDto>>("/api/cart", Json);
        cart!.Data!.Items.Should().ContainSingle()
            .Which.Quantity.Should().Be(5);
    }

    [Fact]
    public async Task AddCartItem_NonExistentProduct_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-add-3@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = 9999,
            Quantity = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task AddCartItem_InvalidQuantity_Returns400ValidationError()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-add-4@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddCartItem_ExceedsStock_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-add-5@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Use the 4th seeded product (Mechanical Keyboard, stock 25)
        var productId = _productIds[3];
        var response = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 100
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    // ─────────────── PUT /api/cart/items/:id ───────────────

    [Fact]
    public async Task UpdateCartItem_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/cart/items/1", new UpdateCartItemRequest
        {
            Quantity = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateCartItem_WithValidQuantity_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-upd-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productId = _productIds[0];
        // Add an item first
        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 2
        });
        var added = await addResponse.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        var cartItemId = added!.Data!.Id;

        // Update the item
        var response = await client.PutAsJsonAsync($"/api/cart/items/{cartItemId}", new UpdateCartItemRequest
        {
            Quantity = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        body!.Data!.Quantity.Should().Be(10);
        body.Data.Id.Should().Be(cartItemId);
    }

    [Fact]
    public async Task UpdateCartItem_NonExistentItemId_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-upd-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/cart/items/9999", new UpdateCartItemRequest
        {
            Quantity = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateCartItem_ExceedsStock_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-upd-3@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add 4th product (Mechanical Keyboard, stock 25)
        var productId = _productIds[3];
        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 1
        });
        var added = await addResponse.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        var cartItemId = added!.Data!.Id;

        var response = await client.PutAsJsonAsync($"/api/cart/items/{cartItemId}", new UpdateCartItemRequest
        {
            Quantity = 100
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task UpdateCartItem_InvalidQuantity_Returns400ValidationError()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-upd-4@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 1
        });
        var added = await addResponse.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        var cartItemId = added!.Data!.Id;

        var response = await client.PutAsJsonAsync($"/api/cart/items/{cartItemId}", new UpdateCartItemRequest
        {
            Quantity = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────── DELETE /api/cart/items/:id ───────────────

    [Fact]
    public async Task RemoveCartItem_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/cart/items/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveCartItem_WithValidId_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-del-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var addResponse = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 1
        });
        var added = await addResponse.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json);
        var cartItemId = added!.Data!.Id;

        var response = await client.DeleteAsync($"/api/cart/items/{cartItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify item is gone
        var cart = await client.GetFromJsonAsync<ApiResponse<CartDto>>("/api/cart", Json);
        cart!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveCartItem_NonExistentItemId_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-del-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/cart/items/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task RemoveCartItem_OnlyRemovesTargetItem()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-del-3@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var productA = _productIds[0];
        var productB = _productIds[1];

        // Add two different items
        var add1 = await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productA,
            Quantity = 1
        });
        var item1 = (await add1.Content.ReadFromJsonAsync<ApiResponse<CartItemDto>>(Json))!.Data!;

        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = productB,
            Quantity = 1
        });

        // Remove only the first
        var response = await client.DeleteAsync($"/api/cart/items/{item1.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await client.GetFromJsonAsync<ApiResponse<CartDto>>("/api/cart", Json);
        cart!.Data!.Items.Should().HaveCount(1);
        cart.Data.Items[0].ProductId.Should().Be(productB);
    }

    // ─────────────── DELETE /api/cart (clear) ───────────────

    [Fact]
    public async Task ClearCart_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClearCart_WithItems_Returns200AndEmptiesCart()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-clr-1@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add items
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[0],
            Quantity = 2
        });
        await client.PostAsJsonAsync("/api/cart/items", new AddCartItemRequest
        {
            ProductId = _productIds[1],
            Quantity = 1
        });

        var response = await client.DeleteAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify cart is empty
        var cart = await client.GetFromJsonAsync<ApiResponse<CartDto>>("/api/cart", Json);
        cart!.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearCart_EmptyCart_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cart-clr-2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─────────────── helpers ───────────────

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Cart Test User"
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
    /// Fetches the product IDs via the public catalog API so tests are
    /// resilient to InMemory auto-increment counter behavior.
    /// </summary>
    private async Task<int[]> GetSeededProductIdsAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products?pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        return body!.Data!.Select(p => p.Id).ToArray();
    }
}
