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
/// <c>Postgres</c> collection (≈ 5 s startup).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VariantStockWriterPostgresTests
{
    private readonly PostgresFixture _fixture;

    public VariantStockWriterPostgresTests(PostgresFixture fixture) => _fixture = fixture;

    private static async Task<(Product Product, ProductVariant Variant)> SeedVariantAsync(
        ApplicationDbContext ctx, int stock, string skuSuffix)
    {
        var category = await ctx.Categories.FirstAsync();
        var product = new Product
        {
            Name = $"Widget-{skuSuffix}-{Guid.NewGuid():N}",
            Slug = $"widget-{skuSuffix.ToLowerInvariant()}-{Guid.NewGuid():N}",
            Description = "Test product",
            Price = 9.99m,
            CategoryId = category.Id,
            IsActive = true,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Sku = $"SKU-{skuSuffix}-{Guid.NewGuid():N}",
            Stock = stock,
            IsActive = true,
        };
        ctx.ProductVariants.Add(variant);
        await ctx.SaveChangesAsync();
        return (product, variant);
    }

    // ─────────────── Concurrent deduct — last unit ───────────────

    [Fact]
    public async Task TryDeductAsync_Concurrent_LastUnit_ExactlyOneSucceeds()
    {
        // Arrange — one variant with Stock = 1.
        int variantId;
        {
            var opts = _fixture.NewOptions();
            await using var seedCtx = new ApplicationDbContext(opts);
            var (_, v) = await SeedVariantAsync(seedCtx, stock: 1, skuSuffix: "CONCURRENT-1");
            variantId = v.Id;
        }

        // Act — two concurrent deductions against the same row, each with its
        // own DbContext/connection (otherwise EF would serialize on the same
        // context). The atomic UPDATE guard must let exactly one through.
        var taskA = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 1);
        });

        var taskB = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 1);
        });

        var results = await Task.WhenAll(taskA, taskB);

        // Assert — exactly one succeeded, the other was rejected, final stock 0.
        results.Count(r => r).Should().Be(1, "atomic UPDATE must let exactly one concurrent deduction win for the last unit");
        results.Count(r => !r).Should().Be(1);

        {
            var opts = _fixture.NewOptions();
            await using var verifyCtx = new ApplicationDbContext(opts);
            var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
            variant!.Stock.Should().Be(0);
        }
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_TwoUnits_TwoDeducts_BothSucceed()
    {
        int variantId;
        {
            var opts = _fixture.NewOptions();
            await using var seedCtx = new ApplicationDbContext(opts);
            var (_, v) = await SeedVariantAsync(seedCtx, stock: 2, skuSuffix: "CONCURRENT-2");
            variantId = v.Id;
        }

        var taskA = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 1);
        });

        var taskB = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 1);
        });

        var results = await Task.WhenAll(taskA, taskB);

        results.Should().AllSatisfy(r => r.Should().BeTrue("both deductions should succeed when stock is 2"));

        {
            var opts = _fixture.NewOptions();
            await using var verifyCtx = new ApplicationDbContext(opts);
            var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
            variant!.Stock.Should().Be(0);
        }
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_TwoUnits_ThreeDeducts_ExactlyTwoSucceed()
    {
        int variantId;
        {
            var opts = _fixture.NewOptions();
            await using var seedCtx = new ApplicationDbContext(opts);
            var (_, v) = await SeedVariantAsync(seedCtx, stock: 2, skuSuffix: "CONCURRENT-3");
            variantId = v.Id;
        }

        var tasks = Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 1);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r).Should().Be(2, "only two deductions can win when stock is 2");
        results.Count(r => !r).Should().Be(1);

        {
            var opts = _fixture.NewOptions();
            await using var verifyCtx = new ApplicationDbContext(opts);
            var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
            variant!.Stock.Should().Be(0);
        }
    }

    [Fact]
    public async Task TryDeductAsync_Concurrent_InsufficientStock_BothFail()
    {
        int variantId;
        {
            var opts = _fixture.NewOptions();
            await using var seedCtx = new ApplicationDbContext(opts);
            var (_, v) = await SeedVariantAsync(seedCtx, stock: 1, skuSuffix: "CONCURRENT-4");
            variantId = v.Id;
        }

        // Both try to deduct 2 when only 1 is available — both must fail.
        var taskA = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 2);
        });

        var taskB = Task.Run(async () =>
        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            return await sut.TryDeductAsync(variantId, 2);
        });

        var results = await Task.WhenAll(taskA, taskB);

        results.Should().AllSatisfy(r => r.Should().BeFalse());

        {
            var opts = _fixture.NewOptions();
            await using var verifyCtx = new ApplicationDbContext(opts);
            var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
            variant!.Stock.Should().Be(1, "stock must be unchanged when both deductions fail");
        }
    }

    [Fact]
    public async Task TryDeductAsync_ThenRestock_RoundTrips_OnPostgres()
    {
        int variantId;
        {
            var opts = _fixture.NewOptions();
            await using var seedCtx = new ApplicationDbContext(opts);
            var (_, v) = await SeedVariantAsync(seedCtx, stock: 5, skuSuffix: "ROUNDTRIP");
            variantId = v.Id;
        }

        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            (await sut.TryDeductAsync(variantId, 3)).Should().BeTrue();
        }

        {
            var opts = _fixture.NewOptions();
            await using var ctx = new ApplicationDbContext(opts);
            var sut = new VariantStockWriter(ctx);
            await sut.RestockAsync(variantId, 3);
        }

        {
            var opts = _fixture.NewOptions();
            await using var verifyCtx = new ApplicationDbContext(opts);
            var variant = await verifyCtx.ProductVariants.FindAsync(variantId);
            variant!.Stock.Should().Be(5);
        }
    }
}
