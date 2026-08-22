using FluentAssertions;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// TDD-driven tests for <see cref="OrderItemNameFormatter"/>. Pin the contract
/// from CONTEXT.md → OrderItem and ADR 0003 §Consequences:
///   "Product Name" for no-attribute variants,
///   "Product Name (Color)" for color only,
///   "Product Name (Color, Size)" for both, with Color first.
///
/// Both pieces of the spec are pinned here: the format itself, and the order
/// of the attributes (Color before Size). A regression on the order would
/// silently change the OrderItem snapshot without breaking any other test.
/// </summary>
public class OrderItemNameFormatterTests
{
    private static Product NewProduct(string name = "Men's Crew-Neck T-Shirt") => new()
    {
        Id = 1,
        Name = name,
        Slug = "mens-tshirt",
        Description = "test",
        Price = 24.99m,
    };

    private static ProductVariant NewVariant(string? color = null, string? size = null) => new()
    {
        Id = 10,
        ProductId = 1,
        Sku = "TS-001",
        Color = color,
        Size = size,
        Stock = 5,
        IsActive = true,
    };

    [Fact]
    public void Format_NoAttributes_ReturnsPlainProductName()
    {
        var product = NewProduct();
        var variant = NewVariant();

        var result = OrderItemNameFormatter.Format(product, variant);

        result.Should().Be("Men's Crew-Neck T-Shirt");
    }

    [Fact]
    public void Format_ColorOnly_ReturnsProductNameWithColor()
    {
        var product = NewProduct();
        var variant = NewVariant(color: "Black");

        var result = OrderItemNameFormatter.Format(product, variant);

        result.Should().Be("Men's Crew-Neck T-Shirt (Black)");
    }

    [Fact]
    public void Format_SizeOnly_ReturnsProductNameWithSize()
    {
        var product = NewProduct();
        var variant = NewVariant(size: "M");

        var result = OrderItemNameFormatter.Format(product, variant);

        result.Should().Be("Men's Crew-Neck T-Shirt (M)");
    }

    [Fact]
    public void Format_BothAttributes_ReturnsColorBeforeSize()
    {
        // CONTEXT.md → OrderItem: "'Name (Color, Size)' when the variant has
        // attributes." Order matters — pin it here so a future refactor can't
        // flip it without breaking the test.
        var product = NewProduct();
        var variant = NewVariant(color: "Black", size: "M");

        var result = OrderItemNameFormatter.Format(product, variant);

        result.Should().Be("Men's Crew-Neck T-Shirt (Black, M)");
    }
}
