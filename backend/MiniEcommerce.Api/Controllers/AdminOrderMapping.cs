using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

/// <summary>
/// Static mapper from <see cref="Order"/> / <see cref="OrderItem"/> to the
/// admin-facing response DTOs. Centralised so the list, detail, and
/// upcoming status-update endpoints (15a/15b/15c) share one projection
/// and any future field change touches one place.
/// </summary>
internal static class AdminOrderMapping
{
    public static AdminOrderListItem ToListItem(Order o) => new()
    {
        Id = o.Id,
        CustomerId = o.CustomerId,
        CustomerEmail = o.Customer?.Email ?? string.Empty,
        Status = o.Status.ToString(),
        Total = o.Total,
        ItemCount = o.Items.Sum(i => i.Quantity),
        CreatedAt = o.CreatedAt
    };

    public static AdminOrderDetail ToDetail(Order o) => new()
    {
        Id = o.Id,
        Status = o.Status.ToString(),
        Subtotal = o.Subtotal,
        ShippingFee = o.ShippingFee,
        Total = o.Total,
        ShippingFullName = o.ShippingFullName,
        ShippingStreet = o.ShippingStreet,
        ShippingCity = o.ShippingCity,
        ShippingPostalCode = o.ShippingPostalCode,
        ShippingCountry = o.ShippingCountry,
        ShippingPhone = o.ShippingPhone,
        CreatedAt = o.CreatedAt,
        Customer = new AdminOrderCustomer
        {
            Id = o.CustomerId,
            Email = o.Customer?.Email ?? string.Empty,
            FullName = o.Customer?.FullName ?? string.Empty
        },
        // Per spec (issue #7): "subtotal is unitPrice * quantity computed
        // server-side." The result is a fixed historical value snapshotted
        // onto the DTO via init-only; see CONTEXT.md rule #10.
        Items = o.Items.Select(ToItem).ToList()
    };

    public static AdminOrderItemDto ToItem(OrderItem i) => new()
    {
        Id = i.Id,
        ProductId = i.ProductId,
        ProductName = i.ProductName,
        UnitPrice = i.UnitPrice,
        Quantity = i.Quantity,
        Subtotal = i.UnitPrice * i.Quantity
    };
}
