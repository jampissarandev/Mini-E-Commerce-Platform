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

using SixLabors.ImageSharp.Formats.Png;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AdminProductsController</c>. These go through
/// the full ASP.NET Core pipeline using the in-memory database configured by
/// <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AdminProductsControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private int[] _categoryIds = [];
    private int[] _productIds = [];

    public AdminProductsControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();

        using var ctx = _factory.CreateDbContext();
        _categoryIds = await ctx.Categories.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync();
        _productIds = await ctx.Products.OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ═══════════════════════════════════════════════════════════
    //  13a — GET /api/admin/products
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAdminProducts_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminProducts_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-list@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/products");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminProducts_WithAdminToken_ReturnsPaginatedList()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-list@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Count.Should().BeGreaterThan(0);
        body.Meta.Should().NotBeNull();
        body.Meta!.Page.Should().Be(1);
        body.Meta.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAdminProducts_IncludesInactiveProducts()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-inactive@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Soft-delete one product directly in DB
        using (var ctx = _factory.CreateDbContext())
        {
            var product = await ctx.Products.FirstAsync(p => p.Id == _productIds[0]);
            product.IsActive = false;
            await ctx.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/admin/products?pageSize=100");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);

        // Admin list includes inactive products
        body!.Data!.Should().Contain(p => p.Id == _productIds[0]);
        body.Data!.First(p => p.Id == _productIds[0]).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAdminProducts_AdminListItem_HasExpectedFields()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-fields@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/products");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);
        var first = body!.Data!.First();

        first.Id.Should().BeGreaterThan(0);
        first.Name.Should().NotBeNullOrEmpty();
        first.Slug.Should().NotBeNullOrEmpty();
        first.Price.Should().BeGreaterThan(0);
        first.Stock.Should().BeGreaterThan(0);
        first.CategoryName.Should().NotBeNullOrEmpty();
        first.CreatedAt.Should().BeAfter(DateTime.MinValue);
    }

    [Fact]
    public async Task GetAdminProducts_SearchFiltersByName()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-search@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/products?q=Headphones");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);

        body!.Data!.Should().HaveCount(1);
        body.Data![0].Name.Should().Contain("Headphones");
    }

    [Fact]
    public async Task GetAdminProducts_IsActiveFilter_True()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-filter@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Soft-delete one product
        using (var ctx = _factory.CreateDbContext())
        {
            var product = await ctx.Products.FirstAsync(p => p.Id == _productIds[0]);
            product.IsActive = false;
            await ctx.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/admin/products?isActive=true");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);

        body!.Data!.Should().OnlyContain(p => p.IsActive);
        body.Data!.Should().NotContain(p => p.Id == _productIds[0]);
    }

    [Fact]
    public async Task GetAdminProducts_IsActiveFilter_False()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-filter2@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Soft-delete one product
        using (var ctx = _factory.CreateDbContext())
        {
            var product = await ctx.Products.FirstAsync(p => p.Id == _productIds[0]);
            product.IsActive = false;
            await ctx.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/admin/products?isActive=false");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);

        body!.Data!.Should().HaveCount(1);
        body.Data![0].Id.Should().Be(_productIds[0]);
        body.Data![0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetAdminProducts_Pagination_Works()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-page@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/products?page=1&pageSize=3");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);

        body!.Data!.Count.Should().Be(3);
        body.Meta!.PageSize.Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════
    //  13a — POST /api/admin/products
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateProduct_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest
        {
            Name = "Test Product",
            Description = "A test product",
            Price = 19.99m,
            Stock = 10,
            CategoryId = _categoryIds[0]
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_WithCustomerToken_Returns403()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, "cust-create@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/products", new CreateProductRequest
        {
            Name = "Test Product",
            Description = "A test product",
            Price = 19.99m,
            Stock = 10,
            CategoryId = _categoryIds[0]
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProduct_WithValidData_Returns201WithProduct()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-create@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateProductRequest
        {
            Name = "Gaming Mouse",
            Description = "High-precision wireless gaming mouse",
            Price = 59.99m,
            Stock = 42,
            CategoryId = _categoryIds[0]
        };

        var response = await client.PostAsJsonAsync("/api/admin/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductDetailDto>>(Json);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.Name.Should().Be("Gaming Mouse");
        body.Data.Slug.Should().Be("gaming-mouse");
        body.Data.Description.Should().Be("High-precision wireless gaming mouse");
        body.Data.Price.Should().Be(59.99m);
        body.Data.Stock.Should().Be(42);
        body.Data.IsActive.Should().BeTrue();
        body.Data.Category.Id.Should().Be(_categoryIds[0]);
        body.Data.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateProduct_WithCustomSlug_UsesProvidedSlug()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-slug@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateProductRequest
        {
            Name = "Custom Slug Product",
            Slug = "my-custom-slug",
            Description = "Product with custom slug",
            Price = 29.99m,
            Stock = 10,
            CategoryId = _categoryIds[0]
        };

        var response = await client.PostAsJsonAsync("/api/admin/products", request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductDetailDto>>(Json);

        body!.Data!.Slug.Should().Be("my-custom-slug");
    }

    [Fact]
    public async Task CreateProduct_DuplicateSlug_Returns409()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-dup-slug@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The seed data has a product with slug "wireless-headphones"
        var request = new CreateProductRequest
        {
            Name = "Wireless Headphones",
            Description = "Another wireless headphones product",
            Price = 99.99m,
            Stock = 5,
            CategoryId = _categoryIds[0]
        };

        var response = await client.PostAsJsonAsync("/api/admin/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("SLUG_TAKEN");
    }

    [Fact]
    public async Task CreateProduct_WithExplicitIsActiveFalse_CreatesInactiveProduct()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-inactive-create@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateProductRequest
        {
            Name = "Draft Product",
            Description = "This is a draft",
            Price = 10.00m,
            Stock = 1,
            CategoryId = _categoryIds[0],
            IsActive = false
        };

        var response = await client.PostAsJsonAsync("/api/admin/products", request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductDetailDto>>(Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body!.Data!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProduct_NonExistentCategory_Returns400()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-bad-cat@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateProductRequest
        {
            Name = "Bad Category Product",
            Description = "Product with non-existent category",
            Price = 10.00m,
            Stock = 1,
            CategoryId = 9999
        };

        var response = await client.PostAsJsonAsync("/api/admin/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("INVALID_CATEGORY");
    }

    // ═══════════════════════════════════════════════════════════
    //  13b — PUT /api/admin/products/:id
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateProduct_WithValidData_Returns200WithUpdatedProduct()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-update@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new UpdateProductRequest
        {
            Name = "Updated Headphones",
            Description = "Updated description for headphones",
            Price = 89.99m,
            Stock = 100,
            CategoryId = _categoryIds[0]
        };

        var response = await client.PutAsJsonAsync($"/api/admin/products/{_productIds[0]}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductDetailDto>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Name.Should().Be("Updated Headphones");
        body.Data.Description.Should().Be("Updated description for headphones");
        body.Data.Price.Should().Be(89.99m);
        body.Data.Stock.Should().Be(100);
    }

    [Fact]
    public async Task UpdateProduct_ChangesPersist()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-persist@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.PutAsJsonAsync($"/api/admin/products/{_productIds[0]}", new UpdateProductRequest
        {
            Name = "Persisted Name",
            Description = "Persisted description",
            Price = 42.00m,
            Stock = 7,
            CategoryId = _categoryIds[0]
        });

        // Verify via admin list
        var response = await client.GetAsync("/api/admin/products");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);
        var updated = body!.Data!.First(p => p.Id == _productIds[0]);

        updated.Name.Should().Be("Persisted Name");
        updated.Price.Should().Be(42.00m);
        updated.Stock.Should().Be(7);
    }

    [Fact]
    public async Task UpdateProduct_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-update-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/admin/products/99999", new UpdateProductRequest
        {
            Name = "Ghost Product",
            Description = "Does not exist",
            Price = 1.00m,
            Stock = 1,
            CategoryId = _categoryIds[0]
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateProduct_SlugChange_Persists()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-slug-update@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/admin/products/{_productIds[0]}", new UpdateProductRequest
        {
            Name = "New Slug Product",
            Slug = "completely-new-slug",
            Description = "Updated",
            Price = 10.00m,
            Stock = 5,
            CategoryId = _categoryIds[0]
        });

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductDetailDto>>(Json);
        body!.Data!.Slug.Should().Be("completely-new-slug");
    }

    [Fact]
    public async Task UpdateProduct_DuplicateSlug_Returns409()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-dup-update@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Try to update product[1] with the slug of product[0]
        var targetProduct = await GetProductSlugAsync(_productIds[0]);
        var response = await client.PutAsJsonAsync($"/api/admin/products/{_productIds[1]}", new UpdateProductRequest
        {
            Name = "Collision",
            Slug = targetProduct,
            Description = "Slug collision",
            Price = 5.00m,
            Stock = 1,
            CategoryId = _categoryIds[0]
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("SLUG_TAKEN");
    }

    // ═══════════════════════════════════════════════════════════
    //  13b — DELETE /api/admin/products/:id
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteProduct_SoftDelete_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-soft-del@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeletedProduct_IsInactive()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-soft-check@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.DeleteAsync($"/api/admin/products/{_productIds[0]}");

        // Verify in admin list (large page to ensure all products returned)
        var response = await client.GetAsync($"/api/admin/products?pageSize=100&q={Uri.EscapeDataString((await GetProductNameAsync(_productIds[0]))!)}");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdminProductListItem>>>(Json);
        body!.Data!.Should().ContainSingle();
        body.Data![0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteProduct_SoftDeleted_NotInPublicCatalog()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-soft-public@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.DeleteAsync($"/api/admin/products/{_productIds[0]}");

        // Verify via public endpoint (use unauthenticated client)
        var publicClient = _factory.CreateClient();
        var response = await publicClient.GetAsync("/api/products");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);

        body!.Data!.Should().NotContain(p => p.Id == _productIds[0]);
    }

    [Fact]
    public async Task DeleteProduct_HardDelete_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-hard-del@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}?hard=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify product is gone from DB
        using var ctx = _factory.CreateDbContext();
        var product = await ctx.Products.FindAsync(_productIds[0]);
        product.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProduct_HardDelete_InUse_Returns409()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-hard-use@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Add the product to a cart so it's "in use"
        using (var ctx = _factory.CreateDbContext())
        {
            var user = new ApplicationUser
            {
                UserName = "cart-user@test.com",
                Email = "cart-user@test.com",
                FullName = "Cart User",
                EmailConfirmed = true
            };
            var userManager = _factory.Services.GetRequiredService<UserManager<ApplicationUser>>();
            await userManager.CreateAsync(user, "Password123!");
            await userManager.AddToRoleAsync(user, "Customer");

            var cart = new Cart { UserId = user.Id };
            ctx.Carts.Add(cart);
            await ctx.SaveChangesAsync();

            ctx.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                ProductId = _productIds[0],
                Quantity = 1,
                UnitPrice = 10m
            });
            await ctx.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}?hard=true");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("PRODUCT_IN_USE");
    }

    [Fact]
    public async Task DeleteProduct_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-del-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/admin/products/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ═══════════════════════════════════════════════════════════
    //  13c — POST /api/admin/products/:id/images
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task UploadImage_WithValidImage_Returns201()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-img-upload@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var imageContent = CreateTestImageContent("test-image.png");
        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "files", "test-image.png");

        var response = await client.PostAsync($"/api/admin/products/{_productIds[0]}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductImageDto>>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Should().HaveCount(1);
        body.Data![0].Url.Should().NotBeNullOrEmpty();
        body.Data![0].SortOrder.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task UploadImage_ProductNotFound_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-img-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var imageContent = CreateTestImageContent("test.png");
        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "files", "test.png");

        var response = await client.PostAsync("/api/admin/products/99999/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task UploadImage_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        using var imageContent = CreateTestImageContent("test.png");
        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "files", "test.png");

        var response = await client.PostAsync($"/api/admin/products/{_productIds[0]}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadImage_WithMultipleFiles_Returns201WithAllImages()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-img-multi@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var image1 = CreateTestImageContent("img1.png");
        using var image2 = CreateTestImageContent("img2.png");
        using var form = new MultipartFormDataContent();
        form.Add(image1, "files", "img1.png");
        form.Add(image2, "files", "img2.png");

        var response = await client.PostAsync($"/api/admin/products/{_productIds[0]}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductImageDto>>>(Json);
        body!.Data!.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════
    //  13c — DELETE /api/admin/products/:id/images/:imageId
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task DeleteImage_WithValidId_Returns200()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-img-del@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Upload an image first
        using var imageContent = CreateTestImageContent("to-delete.png");
        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "files", "to-delete.png");

        var uploadResponse = await client.PostAsync($"/api/admin/products/{_productIds[0]}/images", form);
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProductImageDto>>>(Json);
        var imageId = uploadBody!.Data![0].Id;

        // Delete the image
        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}/images/{imageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify image is removed from product detail
        using var ctx = _factory.CreateDbContext();
        var exists = await ctx.ProductImages.AnyAsync(pi => pi.Id == imageId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteImage_NonExistent_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await CreateAdminAndLoginAsync(client, "admin-img-del-404@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}/images/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("IMAGE_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteImage_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/admin/products/{_productIds[0]}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Registers a regular customer and returns the JWT.
    /// </summary>
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

    /// <summary>
    /// Creates an admin user via the API, promotes them to Admin role via
    /// UserManager, and returns a JWT with the Admin claim.
    /// </summary>
    private async Task<string> CreateAdminAndLoginAsync(HttpClient client, string email)
    {
        // Register as a normal customer first
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Admin User"
        });

        // Promote to Admin via UserManager
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        // Login — the JWT now carries the Admin role claim
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        return body!.Data!.Token;
    }

    private async Task<string> GetProductSlugAsync(int productId)
    {
        using var ctx = _factory.CreateDbContext();
        var product = await ctx.Products.FindAsync(productId);
        return product!.Slug;
    }

    private async Task<string?> GetProductNameAsync(int productId)
    {
        using var ctx = _factory.CreateDbContext();
        var product = await ctx.Products.FindAsync(productId);
        return product?.Name;
    }

    /// <summary>
    /// Creates a minimal valid PNG image as multipart form content.
    /// </summary>
    private static StreamContent CreateTestImageContent(string fileName)
    {
        // Create a minimal 1x1 white PNG in memory using ImageSharp.
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
        image.ProcessPixelRows(accessor =>
        {
            accessor.GetRowSpan(0)[0] = new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255, 255);
        });
        var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        var content = new StreamContent(ms);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return content;
    }
}
