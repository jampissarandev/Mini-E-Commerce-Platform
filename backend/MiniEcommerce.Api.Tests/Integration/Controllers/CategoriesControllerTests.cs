using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>CategoriesController</c> (Task 7c).
/// Covers: the full category list with per-category active-product counts,
/// and that the count excludes soft-deleted (inactive) products.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class CategoriesControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CategoriesControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _factory.SeedCatalogDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetCategories_ReturnsAllCategoriesWithActiveProductCounts()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CategoryDto>>>(Json);
        body!.Success.Should().BeTrue();
        body!.Data.Should().HaveCount(5);

        // Seed data: 5 categories x 4 products each.
        var electronics = body.Data!.Single(c => c.Slug == "electronics");
        electronics.Name.Should().Be("Electronics");
        electronics.ProductCount.Should().Be(4);

        var books = body.Data!.Single(c => c.Slug == "books");
        books.ProductCount.Should().Be(4);
    }

    [Fact]
    public async Task GetCategories_ProductCountExcludesInactiveProducts()
    {
        // Soft-delete one Electronics product directly in the DB.
        using (var context = _factory.CreateDbContext())
        {
            var product = await context.Products
                .Include(p => p.Category)
                .FirstAsync(p => p.Category.Slug == "electronics");
            product.IsActive = false;
            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/categories");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<CategoryDto>>>(Json);

        var electronics = body!.Data!.Single(c => c.Slug == "electronics");
        electronics.ProductCount.Should().Be(3);
    }
}