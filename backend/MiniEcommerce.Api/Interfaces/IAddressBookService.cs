namespace MiniEcommerce.Api.Interfaces;

/// <summary>
/// Single source of truth for the ADR 0004 invariant:
/// a customer has at-most-one <c>IsDefault=true</c> address.
/// All three rules that mutate <c>IsDefault</c> live here so the controllers
/// don't duplicate the invariant:
///   <list type="bullet">
///     <item><see cref="CreateForCustomerAsync"/> — first address auto-defaults.</item>
///     <item><see cref="SetDefaultAsync"/> — unset peer defaults, set target.</item>
///     <item><see cref="DeleteAsync"/> — if the deleted address was default, promote most-recent remaining.</item>
///   </list>
/// </summary>
public interface IAddressBookService
{
    /// <summary>
    /// Create a new address for the customer. The first address is auto-default;
    /// all later addresses are non-default. Returns the persisted address.
    /// </summary>
    Task<Models.Address> CreateForCustomerAsync(
        string customerId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the editable fields of an existing address (everything except
    /// <c>IsDefault</c> — use <see cref="SetDefaultAsync"/> for that).
    /// Returns <c>true</c> if the address was found and updated; <c>false</c>
    /// if no address with that id belongs to the customer (callers should map
    /// to <c>404 ADDRESS_NOT_FOUND</c>).
    /// </summary>
    Task<bool> UpdateAsync(
        string customerId,
        int addressId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark an existing address as the customer's default. Unsets any other
    /// default for that customer. Returns <c>true</c> if the address was found
    /// and updated; <c>false</c> if no address with that id belongs to the
    /// customer (callers should map to <c>404 ADDRESS_NOT_FOUND</c>).
    /// </summary>
    Task<bool> SetDefaultAsync(
        string customerId,
        int addressId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an address. If the deleted address was the default, the
    /// most-recent remaining address is promoted. Returns <c>true</c> if the
    /// address was found and deleted; <c>false</c> if no address with that id
    /// belongs to the customer (callers should map to <c>404 ADDRESS_NOT_FOUND</c>).
    /// </summary>
    Task<bool> DeleteAsync(
        string customerId,
        int addressId,
        CancellationToken cancellationToken = default);
}
