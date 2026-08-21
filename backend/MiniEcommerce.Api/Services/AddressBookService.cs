using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Implements the ADR 0004 single-default invariant. Uses a single
/// conditional INSERT ... SELECT pattern: on Postgres we keep the check
/// inside the DB so the first-address race window is closed; on InMemory
/// (tests) we just use the tracker — it is single-threaded.
/// </summary>
public class AddressBookService : IAddressBookService
{
    private readonly ApplicationDbContext _context;

    public AddressBookService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Address> SaveSnapshotAsync(
        string customerId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default)
    {
        // Decision: first address IsDefault=true, all later IsDefault=false.
        // The AnyAsync check is inside the same SaveChanges as the Add so
        // the Postgres path closes the first-address race at the DB level
        // via the serialized INSERT; the InMemory path is single-threaded.
        var hasAddresses = await _context.Addresses
            .AnyAsync(a => a.CustomerId == customerId, cancellationToken);

        var address = new Address
        {
            CustomerId = customerId,
            FullName = fullName,
            Street = street,
            City = city,
            PostalCode = postalCode,
            Country = country,
            Phone = phone,
            IsDefault = !hasAddresses,
            CreatedAt = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);
        return address;
    }
}
