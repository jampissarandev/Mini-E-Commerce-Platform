using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for the public <c>ProductsController</c> catalog
/// list. Pins the minimal variant summary (<c>{id, stock}</c>) that the
/// frontend <c>ProductCard</c> quick-add needs (ADR 0008 Phase C) without
/// pulling the full detail payload (Sku/Size/Color stay detail-only per the
/// Task 27b "keep the list payload small" decision).
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
    public async Task GetProducts_MultiVariantProduct_CarriesIdAndStockSummaryInVariantIdOrder()
    {
        // Arrange — Classic T-Shirt gets three active variants with known
        // stocks (50 / 30 / 0) from SeedVariantsWithAttributesAsync.
        var tshirtId = await _factory.SeedVariantsWithAttributesAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var tshirt = body!.Data!.Should().ContainSingle(p => p.Id == tshirtId).Subject;

        tshirt.VariantCount.Should().Be(3);

        // Wire shape: one entry per active variant, carrying only id + stock.
        // Expected stocks are the seed literals (independent source of truth),
        // not recomputed from the entity.
        tshirt.Variants.Should().HaveCount(3);
        tshirt.Variants.Select(v => v.Stock).Should().Equal(50, 30, 0);

        // Ordering matters: pickPreferredVariant takes the first entry, so the
        // wire order must be deterministic (variant Id ascending — same rule
        // as the detail endpoint).
        int[] wireIds;
        using (var context = _factory.CreateDbContext())
        {
            wireIds = tshirt.Variants.Select(v => v.Id).ToArray();
            var dbIdsOrdered = await context.ProductVariants
                .Where(v => v.ProductId == tshirtId)
                .OrderBy(v => v.Id)
                .Select(v => v.Id)
                .ToArrayAsync();
            wireIds.Should().Equal(dbIdsOrdered);
        }
    }

    [Fact]
    public async Task GetProducts_InactiveVariant_IsExcludedFromSummary()
    {
        // Arrange — deactivate the middle variant (TS-WHT-L, stock 30).
        var tshirtId = await _factory.SeedVariantsWithAttributesAsync();
        using (var context = _factory.CreateDbContext())
        {
            var variant = await context.ProductVariants
                .FirstAsync(v => v.ProductId == tshirtId && v.Sku == "TS-WHT-L");
            variant.IsActive = false;
            await context.SaveChangesAsync();
        }
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert — only the two active variants appear, and the count agrees.
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var tshirt = body!.Data!.Should().ContainSingle(p => p.Id == tshirtId).Subject;

        tshirt.VariantCount.Should().Be(2);
        tshirt.Variants.Should().HaveCount(2);
        tshirt.Variants.Select(v => v.Stock).Should().Equal(50, 0);
    }

    [Fact]
    public async Task GetProducts_SingleLegacyVariant_StillCarriesOneElementSummary()
    {
        // Arrange — every seeded product has exactly one LEGACY variant
        // ("Wireless Headphones", stock 50). Quick-add must work on
        // single-variant cards too, so the summary cannot be empty there.
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/products");

        // Assert
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProductListItem>>>(Json);
        var headphones = body!.Data!.Should().ContainSingle(p => p.Slug == "wireless-headphones").Subject;

        headphones.VariantCount.Should().Be(1);
        headphones.Variants.Should().ContainSingle();
        headphones.Variants[0].Stock.Should().Be(50);
    }
}
