namespace MiniEcommerce.Api.Interfaces;

/// <summary>
/// Single source of truth for the ADR 0004 invariant:
/// a customer has at-most-one <c>IsDefault=true</c> address.
/// The "first address auto-defaults" rule lives here so
/// <see cref="Controllers.AddressesController"/> and
/// <see cref="Controllers.OrdersController"/> don't duplicate it.
/// </summary>
public interface IAddressBookService
{
    Task<Models.Address> SaveSnapshotAsync(
        string customerId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default);
}
