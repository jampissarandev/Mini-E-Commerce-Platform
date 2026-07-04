using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>ProductsController</c> and <c>CategoriesController</c>.
/// These go through the full ASP.NET Core pipeline using the in-memory database
/// configured by <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class ProductsControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductsControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── GET /api/products ───────────────

    [Fact]
    public async Task GetProducts_ReturnsPaginatedList()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().BeGreaterThan(0);
        body.Meta.Should().NotBeNull();
        body.Meta!.Page.Should().Be(1);
        body.Meta.PageSize.Should().Be(10);
        body.Meta.TotalCount.Should().BeGreaterThan(0);
        body.Meta.TotalPages.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProducts_ReturnsExpectedFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var first = body!.Data!.First();
        first.Id.Should().BeGreaterThan(0);
        first.Name.Should().NotBeNullOrEmpty();
        first.Slug.Should().NotBeNullOrEmpty();
        first.Price.Should().BeGreaterThan(0);
        first.CategoryName.Should().NotBeNullOrEmpty();
        first.ImageUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetProducts_WithPageSize_LimitsResults()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?pageSize=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Count.Should().Be(3);
        body.Meta!.PageSize.Should().Be(3);
    }

    [Fact]
    public async Task GetProducts_WithPage_ReturnsCorrectPage()
    {
        var client = _factory.CreateClient();

        var page1 = await client.GetAsync("/api/products?pageSize=5&page=1");
        var page2 = await client.GetAsync("/api/products?pageSize=5&page=2");

        var body1 = await page1.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var body2 = await page2.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);

        body1!.Data!.Should().NotBeEmpty();
        body2!.Data!.Should().NotBeEmpty();
        // Pages should have different items
        var page1Ids = body1.Data!.Select(p => p.Id).ToList();
        var page2Ids = body2.Data!.Select(p => p.Id).ToList();
        page1Ids.Should().NotIntersectWith(page2Ids);
    }

    [Fact]
    public async Task GetProducts_WithCategoryFilter_ReturnsOnlyMatchingProducts()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?category=electronics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().OnlyContain(p => p.CategoryName == "Electronics");
        body.Data.Should().HaveCount(4); // Seed has 4 electronics products
    }

    [Fact]
    public async Task GetProducts_WithSearchFilter_ReturnsMatchingProducts()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?search=headphones");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().ContainSingle(p => p.Slug == "wireless-headphones");
    }

    [Fact]
    public async Task GetProducts_WithSortByPriceAsc_ReturnsCheapestFirst()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?sort=price_asc");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var prices = body!.Data!.Select(p => p.Price).ToList();
        prices.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetProducts_WithSortByPriceDesc_ReturnsExpensiveFirst()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?sort=price_desc");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var prices = body!.Data!.Select(p => p.Price).ToList();
        prices.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetProducts_WithSortByNameAsc_ReturnsAlphabetical()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?sort=name_asc");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var names = body!.Data!.Select(p => p.Name).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetProducts_WithSortByNewest_ReturnsMostRecentlyCreatedFirst()
    {
        // InMemory provider has weak ordering semantics on DateTime columns,
        // so we make the timestamps unique AND far apart from any other
        // product's timestamp to avoid ties. We move all but two products to
        // 2000, then set our two targets to 2020 and 2026 — guaranteeing the
        // top two are exactly the ones we control.
        var client = _factory.CreateClient();
        int olderId;
        int newerId;
        using (var db = _factory.CreateDbContext())
        {
            var targets = db.Products.OrderBy(p => p.Id).Take(2).ToList();
            olderId = targets[0].Id;
            newerId = targets[1].Id;

            foreach (var p in db.Products)
                p.CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            targets[0].CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            targets[1].CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/products?sort=newest&pageSize=100");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.First().Id.Should().Be(newerId);   // 2026 first
        body.Data!.Skip(1).First().Id.Should().Be(olderId); // 2020 second
    }

    [Fact]
    public async Task GetProducts_WithMinPriceFilter_ExcludesCheaperProducts()
    {
        var client = _factory.CreateClient();

        // Seed cheapest products: Atomic Habits + Wool Beanie + Puzzle = 19.99
        var response = await client.GetAsync("/api/products?minPrice=30&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().OnlyContain(p => p.Price >= 30m);
    }

    [Fact]
    public async Task GetProducts_WithMaxPriceFilter_ExcludesMoreExpensiveProducts()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?maxPrice=25&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().OnlyContain(p => p.Price <= 25m);
    }

    [Fact]
    public async Task GetProducts_WithPriceRange_ReturnsOnlyMatchingProducts()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?minPrice=30&maxPrice=50&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().NotBeEmpty();
        body.Data!.Should().OnlyContain(p => p.Price >= 30m && p.Price <= 50m);
    }

    [Fact]
    public async Task GetProducts_ReturnsCacheControlHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GetProducts_WithInvalidPage_ReturnsEmptyList()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?page=999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        body!.Data!.Should().BeEmpty();
    }

    // ─────────────── GET /api/products/{id} ───────────────

    [Fact]
    public async Task GetProductById_WithValidId_ReturnsProduct()
    {
        var client = _factory.CreateClient();

        // First get a valid product ID from the list
        var listResponse = await client.GetAsync("/api/products?pageSize=1");
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var productId = listBody!.Data!.First().Id;

        var response = await client.GetAsync($"/api/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDetailDto>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.Id.Should().Be(productId);
        body.Data.Name.Should().NotBeNullOrEmpty();
        body.Data.Slug.Should().NotBeNullOrEmpty();
        body.Data.Description.Should().NotBeNullOrEmpty();
        body.Data.Price.Should().BeGreaterThan(0);
        body.Data.Stock.Should().BeGreaterThanOrEqualTo(0);
        body.Data.Category.Should().NotBeNull();
        body.Data.Category.Name.Should().NotBeNullOrEmpty();
        body.Data.Images.Should().NotBeNull();
        body.Data.Images!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProductById_WithInvalidId_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task GetProductById_WithInactiveProduct_Returns404()
    {
        // Soft-deleted (IsActive = false) products must be hidden from the
        // public detail endpoint, not silently returned.
        var client = _factory.CreateClient();
        var id = _factory.CreateDbContext().Products.First().Id;
        using (var db = _factory.CreateDbContext())
        {
            var product = db.Products.First(p => p.Id == id);
            product.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────── GET /api/categories ───────────────

    [Fact]
    public async Task GetCategories_ReturnsAllCategories()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CategoryDto>>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().Be(5); // Seed creates 5 categories
        body.Data.Should().OnlyContain(c => c.Name.Length > 0 && c.Slug.Length > 0);
    }

    [Fact]
    public async Task GetCategories_IncludeProductCount()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CategoryDto>>>(Json);
        var electronics = body!.Data!.Single(c => c.Slug == "electronics");
        electronics.ProductCount.Should().Be(4); // Seed has 4 electronics products
    }

    [Fact]
    public async Task GetCategories_ReturnsCacheControlHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
    }
}
