using FluentAssertions;
using MiniEcommerce.Api.Controllers;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Tests.Unit.Controllers;

/// <summary>
/// TDD-driven tests for <see cref="AdminOrderMapping"/>. Pins the contract
/// from CONTEXT.md → OrderItem + ADR 0008 §Consequences:
///
///   - <c>Size</c> and <c>Color</c> on <c>AdminOrderItemDto</c> are populated
///     from the snapshotted <c>OrderItem</c> fields (set at checkout time).
///   - <c>Sku</c> is read from <c>OrderItem.ProductVariant.Sku</c> at
///     mapping time (live navigation property). <c>Sku</c> is admin-only.
///   - The <c>ProductName</c> field is unchanged (still the formatted
///     snapshot string).
///   - When the variant has no <c>Color</c>/<c>Size</c> (legacy variants
///     from migration backfill), the DTO fields are null.
/// </summary>
public class AdminOrderMappingTests
{
    private static Product NewProduct(string name = "Classic T-Shirt") => new()
    {
        Id = 1,
        Name = name,
        Slug = "classic-t-shirt",
        Description = "test",
        Price = 24.99m,
    };

    private static ProductVariant NewVariant(
        string sku = "TS-BLK-M",
        string? color = "Black",
        string? size = "M",
        int stock = 5)
    {
        var variant = new ProductVariant
        {
            Id = 10,
            ProductId = 1,
            Sku = sku,
            Color = color,
            Size = size,
            Stock = stock,
            IsActive = true,
        };
        // Wire up the back-reference so ImageUrl navigation works.
        variant.Product = NewProduct();
        return variant;
    }

    private static OrderItem NewOrderItem(
        ProductVariant? variant = null,
        string? size = "M",
        string? color = "Black") => new()
    {
        Id = 100,
        ProductVariantId = variant?.Id ?? 10,
        ProductName = "Classic T-Shirt (Black, M)",
        UnitPrice = 24.99m,
        Quantity = 2,
        Size = size,
        Color = color,
        ProductVariant = variant ?? NewVariant(),
    };

    [Fact]
    public void ToItem_PopulatesSizeAndColorFromSnapshot()
    {
        var item = NewOrderItem(size: "M", color: "Black");

        var dto = AdminOrderMapping.ToItem(item);

        dto.Size.Should().Be("M");
        dto.Color.Should().Be("Black");
    }

    [Fact]
    public void ToItem_PopulatesSkuFromVariant()
    {
        var item = NewOrderItem(variant: NewVariant(sku: "TS-BLK-M"));

        var dto = AdminOrderMapping.ToItem(item);

        dto.Sku.Should().Be("TS-BLK-M");
    }

    [Fact]
    public void ToItem_KeepsProductNameSnapshotString()
    {
        // ADR 0008: the split fields are additive; ProductName stays.
        var item = NewOrderItem();

        var dto = AdminOrderMapping.ToItem(item);

        dto.ProductName.Should().Be("Classic T-Shirt (Black, M)");
    }

    [Fact]
    public void ToItem_LegacyVariantWithoutColorOrSize_FieldsAreNull()
    {
        // Seed backfill + migration produce LEGACY-{id} variants with no
        // Color/Size. Existing orders placed against them must map to
        // null Size/Color in the DTO (not "" — null is the canonical
        // "attribute not present" signal per CONTEXT.md → ProductVariant).
        var variant = NewVariant(color: null, size: null);
        var item = NewOrderItem(
            variant: variant,
            size: null,    // OrderItem.Size also null — was snapshotted from variant
            color: null);

        var dto = AdminOrderMapping.ToItem(item);

        dto.Size.Should().BeNull();
        dto.Color.Should().BeNull();
        dto.Sku.Should().Be("TS-BLK-M"); // Sku still present
    }

    [Fact]
    public void ToItem_PreservesUnitPriceAndQuantity()
    {
        // Sanity: the snapshot rule for UnitPrice/Quantity is preserved.
        var item = NewOrderItem();
        item.UnitPrice = 24.99m;
        item.Quantity = 3;

        var dto = AdminOrderMapping.ToItem(item);

        dto.UnitPrice.Should().Be(24.99m);
        dto.Quantity.Should().Be(3);
        dto.Subtotal.Should().Be(74.97m);
    }
}