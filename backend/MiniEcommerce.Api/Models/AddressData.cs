namespace MiniEcommerce.Api.Models;

/// <summary>
/// Bundle of the editable address fields used by both
/// <see cref="MiniEcommerce.Api.Interfaces.IAddressBookService.CreateForCustomerAsync"/>
/// and <see cref="MiniEcommerce.Api.Interfaces.IAddressBookService.UpdateAsync"/>.
/// Replaces the previous "7 string parameters" data clump at the seam.
///
/// <para>
/// <c>IsDefault</c> intentionally lives outside this record: setting it has its
/// own dedicated endpoint (<c>SetDefault</c>) and must never be bundled into a
/// generic Update call.
/// </para>
/// </summary>
public sealed record AddressData(
    string FullName,
    string Street,
    string City,
    string PostalCode,
    string Country,
    string Phone);
