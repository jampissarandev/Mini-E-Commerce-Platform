using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Pin-tests for <see cref="VariantStockWriter"/>. Drives the InMemory provider
/// branch (test environment). The Postgres branch is exercised by the
/// Testcontainers-based concurrent test from Task 24.
/// </summary>
public class VariantStockWriterTests
{
    private static (IVariantStockWriter Sut, ApplicationDbContext Ctx) NewSut()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"VariantStockWriterTests_{Guid.NewGuid():N}")
            .Options;
        var ctx = new ApplicationDbContext(options);
        var sut = new VariantStockWriter(ctx);
        return (sut, ctx);
    }

    private static ProductVariant SeedVariant(ApplicationDbContext ctx, int stock)
    {
        var product = new Product
        {
            Name = "Widget",
            Slug = "widget",
            Price = 9.99m,
            CategoryId = 1,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.Products.Add(product);
        ctx.SaveChanges();
        var v = new ProductVariant
        {
            ProductId = product.Id,
            Sku = $"SKU-{product.Id}",
            Stock = stock,
            IsActive = true,
        };
        ctx.ProductVariants.Add(v);
        ctx.SaveChanges();
        return v;
    }

    // ─────────────── TryDeductAsync ───────────────

    [Fact]
    public async Task TryDeductAsync_VariantExists_SufficientStock_ReturnsTrueAndDecrements()
    {
        var (sut, ctx) = NewSut();
        var v = SeedVariant(ctx, stock: 10);

        var ok = await sut.TryDeductAsync(v.Id, 3);

        ok.Should().BeTrue();
        ctx.ProductVariants.Single(x => x.Id == v.Id).Stock.Should().Be(7);
    }

    [Fact]
    public async Task TryDeductAsync_VariantExists_InsufficientStock_ReturnsFalseAndLeavesStock()
    {
        var (sut, ctx) = NewSut();
        var v = SeedVariant(ctx, stock: 2);

        var ok = await sut.TryDeductAsync(v.Id, 5);

        ok.Should().BeFalse();
        ctx.ProductVariants.Single(x => x.Id == v.Id).Stock.Should().Be(2);
    }

    [Fact]
    public async Task TryDeductAsync_VariantExists_ExactStock_ReturnsTrueAndZeroesIt()
    {
        var (sut, ctx) = NewSut();
        var v = SeedVariant(ctx, stock: 3);

        var ok = await sut.TryDeductAsync(v.Id, 3);

        ok.Should().BeTrue();
        ctx.ProductVariants.Single(x => x.Id == v.Id).Stock.Should().Be(0);
    }

    [Fact]
    public async Task TryDeductAsync_VariantDoesNotExist_ReturnsFalse()
    {
        var (sut, _) = NewSut();

        var ok = await sut.TryDeductAsync(variantId: 99999, 1);

        ok.Should().BeFalse();
    }

    // ─────────────── RestockAsync ───────────────

    [Fact]
    public async Task RestockAsync_VariantExists_IncrementsStock()
    {
        var (sut, ctx) = NewSut();
        var v = SeedVariant(ctx, stock: 5);

        await sut.RestockAsync(v.Id, 3);

        ctx.ProductVariants.Single(x => x.Id == v.Id).Stock.Should().Be(8);
    }

    [Fact]
    public async Task RestockAsync_VariantDoesNotExist_NoOp()
    {
        var (sut, _) = NewSut();

        // Should not throw
        await sut.RestockAsync(variantId: 99999, 3);
    }

    // ─────────────── Round-trip ───────────────

    [Fact]
    public async Task TryDeductThenRestock_RoundTripsBackToOriginal()
    {
        var (sut, ctx) = NewSut();
        var v = SeedVariant(ctx, stock: 10);

        (await sut.TryDeductAsync(v.Id, 4)).Should().BeTrue();
        await sut.RestockAsync(v.Id, 4);

        ctx.ProductVariants.Single(x => x.Id == v.Id).Stock.Should().Be(10);
    }
}
