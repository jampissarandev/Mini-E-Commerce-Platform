using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;
using Npgsql;

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
///     The partial unique index <c>IX_Addresses_OneDefaultPerCustomer</c>
///     (added by <c>AddAddressesUniqueDefaultIndex</c>) backstops that race
///     at the DB level: the second INSERT with <c>IsDefault=true</c> fails
///     SQLSTATE 23505 and we retry as non-default.
/// </summary>
public class AddressBookService : IAddressBookService
{
    // Postgres SQLSTATE: unique_violation. The only unique index on the
    // Addresses table that can fire during an INSERT is the partial unique
    // index ON Addresses(CustomerId) WHERE IsDefault, so any 23505 here
    // means "another concurrent insert beat us to the default".
    private const string PostgresUniqueViolationSqlState = "23505";

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
        // First address IsDefault=true, all later IsDefault=false. The
        // check-then-act covers the common case; the partial unique index
        // (Postgres) backstops the concurrent-first-insert race by failing
        // the second INSERT with SQLSTATE 23505, which we catch and retry
        // as non-default.
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
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPostgresUniqueViolation(ex))
        {
            // The partial unique index fired: another concurrent insert
            // beat us to IsDefault=true. Detach the failed entry, flip this
            // row to non-default, and save again.
            _context.Entry(address).State = EntityState.Detached;
            address.IsDefault = false;
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return address;
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolationSqlState;

    public async Task<bool> UpdateAsync(
        string customerId,
        int addressId,
        string fullName,
        string street,
        string city,
        string postalCode,
        string country,
        string phone,
        CancellationToken cancellationToken = default)
    {
        // Editable fields only — IsDefault has its own endpoint (SetDefault)
        // and is intentionally not touched here, so a PUT can never silently
        // change a customer's default.
        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        address.FullName = fullName;
        address.Street = street;
        address.City = city;
        address.PostalCode = postalCode;
        address.Country = country;
        address.Phone = phone;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
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
