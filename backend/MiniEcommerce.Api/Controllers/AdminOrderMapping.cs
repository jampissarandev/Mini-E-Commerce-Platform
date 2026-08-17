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

    /// <summary>
    /// Case-insensitive substring match on a nullable customer field.
    /// Returns <c>false</c> when the field is null. Used by the list
    /// endpoint's free-text <c>q</c> filter to compose an OR over the
    /// searchable customer fields without duplicating the null-guard
    /// and lowercasing shape at every call site.
    /// </summary>
    /// <param name="value">The customer field to match against (e.g. <c>Email</c>, <c>FullName</c>). May be null.</param>
    /// <param name="lowerTerm">The search term, already trimmed and lowercased once by the caller.</param>
    /// <remarks>
    /// Uses <c>ToLower().Contains(...)</c> to match the convention established
    /// in <c>AdminProductsController</c> and keep EF Core translation portable
    /// across the in-memory test provider and Npgsql in production.
    /// </remarks>
    public static bool MatchesSearchTerm(string? value, string lowerTerm) =>
        value != null && value.ToLower().Contains(lowerTerm);
}
