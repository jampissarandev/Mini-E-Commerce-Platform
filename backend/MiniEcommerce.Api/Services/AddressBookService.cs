using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Implements the ADR 0004 single-default invariant. All three rules that
/// mutate <c>IsDefault</c> live here so controllers don't duplicate them.
///
/// Concurrency notes:
///   - On the InMemory provider (tests) the single-threaded execution model
///     means the check-then-act pattern is race-free by construction.
///   - On Postgres, two callers can interleave their <c>AnyAsync</c> SELECTs
///     before either INSERT commits and both observe <c>hasAddresses=false</c>.
///     The check-then-act here handles the common case, and a partial unique
///     index <c>ON Addresses(CustomerId) WHERE IsDefault</c> backstops the
///     concurrent-first-insert race at the DB level. The follow-up migration
///     <c>AddAddressesUniqueDefaultIndex</c> adds that index.
/// </summary>
public class AddressBookService : IAddressBookService
{
    private readonly ApplicationDbContext _context;

    public AddressBookService(ApplicationDbContext context)
    {
        _context = context;
    }

    private bool IsInMemoryProvider =>
        _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

    public async Task<Address> CreateForCustomerAsync(
        string customerId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default)
    {
        // First address IsDefault=true, all later IsDefault=false.
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
            CreatedAt = DateTime.UtcNow,
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);
        return address;
    }

    public async Task<bool> SetDefaultAsync(
        string customerId,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        var isInMemory = IsInMemoryProvider;
        await using var tx = isInMemory ? null : await _context.Database.BeginTransactionAsync(cancellationToken);

        var otherDefaults = await _context.Addresses
            .Where(a => a.CustomerId == customerId && a.Id != addressId && a.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var other in otherDefaults)
        {
            other.IsDefault = false;
        }

        address.IsDefault = true;
        await _context.SaveChangesAsync(cancellationToken);

        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }
        return true;
    }

    public async Task<bool> DeleteAsync(
        string customerId,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        var wasDefault = address.IsDefault;

        // Delete + promote run inside one DB transaction so a concurrent
        // reader never sees the window with zero defaults. The promotion is
        // a single conditional UPDATE on Postgres (atomic at the DB level)
        // and a tracked-entity update on the InMemory provider used by tests
        // (which doesn't support real transactions — InMemory has no
        // concurrency window to close anyway).
        var isInMemory = IsInMemoryProvider;
        await using var tx = isInMemory ? null : await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            await PromoteNextDefaultInternalAsync(customerId, isInMemory, cancellationToken);
        }

        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }
        return true;
    }

    private async Task PromoteNextDefaultInternalAsync(
        string customerId,
        bool isInMemory,
        CancellationToken cancellationToken)
    {
        if (isInMemory)
        {
            var stillHasDefault = await _context.Addresses
                .AnyAsync(a => a.CustomerId == customerId && a.IsDefault, cancellationToken);
            if (!stillHasDefault)
            {
                var next = await _context.Addresses
                    .Where(a => a.CustomerId == customerId)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (next is not null)
                {
                    next.IsDefault = true;
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
        }
        else
        {
            // Single conditional UPDATE: only flips the most-recent remaining
            // row if no default already exists, so it is a no-op for the
            // "delete the only remaining address" case.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE ""Addresses"" SET ""IsDefault"" = true
                   WHERE ""Id"" = (
                     SELECT ""Id"" FROM ""Addresses""
                     WHERE ""CustomerId"" = {customerId}
                     ORDER BY ""CreatedAt"" DESC
                     LIMIT 1
                   ) AND NOT EXISTS (
                     SELECT 1 FROM ""Addresses""
                     WHERE ""CustomerId"" = {customerId} AND ""IsDefault"" = true
                   )",
                cancellationToken);
        }
    }
}
