using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Integration.Postgres;

/// <summary>
/// True concurrent Postgres test for <see cref="VariantStockWriter"/> (ADR 0002,
/// Task 24 hardening). The InMemory provider used by the rest of the suite is
/// single-threaded and cannot prove the atomic <c>UPDATE ... WHERE Stock &gt;= @qty</c>
/// guard — this class uses a real Postgres container via Testcontainers
/// (<see cref="PostgresFixture"/>) and drives concurrent <c>TryDeductAsync</c>
/// calls against the same row. The container is shared across the
/// <c>Postgres</c> collection (≈ 5 s startup). Tagged <c>Integration-DB</c>
/// so the default <c>dotnet test</c> run skips the harness; opt in with
/// <c>dotnet test --filter "Category=Integration-DB"</c>.
/// </summary>
[Trait("Category", "Integration-DB")]
[Collection(PostgresCollection.Name)]
public sealed class VariantStockWriterPostgresTests
{
    private readonly PostgresFixture _fixture;

    public VariantStockWriterPostgresTests(PostgresFixture fixture) => _fixture = fixture;

    // ─────────────────────── Arrange helpers ───────────────────────

    /// <summary>
    /// Seeds a fresh product + variant on the shared container with the given
    /// initial stock and a unique Sku suffix. Returns the new variant id so
    /// tests don't have to plumb a context they won't use again.
    /// </summary>
    private async Task<int> SeedVariantIdAsync(int stock, string skuSuffix)
    {
        var dbOptions = _fixture.NewOptions();
        await using var seedCtx = new ApplicationDbContext(dbOptions);
        var category = await seedCtx.Categories.FirstAsync();

        var product = new Product
        {
            Name = $"Widget-{skuSuffix}-{Guid.NewGuid():N}",
            Slug = $"widget-{skuSuffix.ToLowerInvariant()}-{Guid.NewGuid():N}",
            Description = "Test product",
            Price = 9.99m,
            CategoryId = category.Id,
            IsActive = true,
        };
        seedCtx.Products.Add(product);
        await seedCtx.SaveChangesAsync();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = $"SKU-{skuSuffix}-{Guid.NewGuid():N}",
            Stock = stock,
            IsActive = true,
        };
        seedCtx.ProductVariants.Add(variant);
        await seedCtx.SaveChangesAsync();
        return variant.Id;
    }

    /// <summary>
    /// Reads the current stock for the variant on a fresh DbContext. Separate
    /// from the racing contexts so we observe the committed value, not a
    /// tracked-entity cache.
    /// </summary>
    private async Task<int> ReadStockAsync(int variantId)
    {
        var dbOptions = _fixture.NewOptions();
        await using var verifyCtx = new ApplicationDbContext(dbOptions);
        var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
        return variant!.Stock;
    }

    /// <summary>
    /// Spins up a fresh DbContext + writer and runs <c>TryDeductAsync</c>.
    /// Each racer gets its own context/connection — otherwise EF would
    /// serialise on the same tracked entity and the test would degenerate
    /// to sequential calls.
    /// </summary>
    private Task<bool> RunDeductAsync(int variantId, int quantity) =>
        Task.Run(async () =>
        {
            var dbOptions = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(dbOptions);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, quantity);
        });

    // ─────────────── Concurrent deduct — last unit ───────────────

    [Fact]
    public async Task TryDeductAsync_Concurrent_LastUnit_ExactlyOneSucceeds()
    {
        var variantId = await SeedVariantIdAsync(stock: 1, skuSuffix: "CONCURRENT-1");

        var results = await Task.WhenAll(
            RunDeductAsync(variantId, 1),
            RunDeductAsync(variantId, 1));

        results.Count(r => r).Should().Be(1, "atomic UPDATE must let exactly one concurrent deduction win for the last unit");
        results.Count(r => !r).Should().Be(1);
        (await ReadStockAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_TwoUnits_TwoDeducts_BothSucceed()
    {
        var variantId = await SeedVariantIdAsync(stock: 2, skuSuffix: "CONCURRENT-2");

        var results = await Task.WhenAll(
            RunDeductAsync(variantId, 1),
            RunDeductAsync(variantId, 1));

        results.Should().AllSatisfy(r => r.Should().BeTrue("both deductions should succeed when stock is 2"));
        (await ReadStockAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_TwoUnits_ThreeDeducts_ExactlyTwoSucceed()
    {
        var variantId = await SeedVariantIdAsync(stock: 2, skuSuffix: "CONCURRENT-3");

        var tasks = Enumerable.Range(0, 3).Select(_ => RunDeductAsync(variantId, 1)).ToArray();
        var results = await Task.WhenAll(tasks);

        results.Count(r => r).Should().Be(2, "only two deductions can win when stock is 2");
        results.Count(r => !r).Should().Be(1);
        (await ReadStockAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_InsufficientStock_BothFail()
    {
        var variantId = await SeedVariantIdAsync(stock: 1, skuSuffix: "CONCURRENT-4");

        // Both try to deduct 2 when only 1 is available — both must fail.
        var results = await Task.WhenAll(
            RunDeductAsync(variantId, 2),
            RunDeductAsync(variantId, 2));

        results.Should().AllSatisfy(r => r.Should().BeFalse());
        (await ReadStockAsync(variantId)).Should().Be(1, "stock must be unchanged when both deductions fail");
    }

    [Fact]
    public async Task TryDeductAsync_ThenRestock_RestoresOriginalStock()
    {
        var variantId = await SeedVariantIdAsync(stock: 5, skuSuffix: "ROUNDTRIP");

        var dbOptions = _fixture.NewOptions();
        await using (var ctx = new ApplicationDbContext(dbOptions))
        {
            var sut = new VariantStockWriter(ctx);
            (await sut.TryDeductAsync(variantId, 3)).Should().BeTrue();
        }

        await using (var ctx = new ApplicationDbContext(dbOptions))
        {
            var sut = new VariantStockWriter(ctx);
            await sut.RestockAsync(variantId, 3);
        }

        (await ReadStockAsync(variantId)).Should().Be(5);
    }
}
