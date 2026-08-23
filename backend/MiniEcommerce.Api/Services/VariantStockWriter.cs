using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Interfaces;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Default <see cref="IVariantStockWriter"/> implementation. Picks Postgres
/// atomic SQL on the relational provider, and tracked-entity decrement on
/// the InMemory provider used by tests. Provider is captured at construction
/// (per-request scope) so the branch isn't repeated on every call.
/// </summary>
public class VariantStockWriter : IVariantStockWriter
{
    private readonly ApplicationDbContext _context;
    private readonly bool _isInMemory;

    public VariantStockWriter(ApplicationDbContext context)
    {
        _context = context;
        _isInMemory = context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    }

    public async Task<bool> TryDeductAsync(int variantId, int quantity, CancellationToken cancellationToken = default)
    {
        if (_isInMemory)
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
            if (variant is null || variant.Stock < quantity)
            {
                return false;
            }
            variant.Stock -= quantity;
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ProductVariants\" SET \"Stock\" = \"Stock\" - {quantity} WHERE \"Id\" = {variantId} AND \"Stock\" >= {quantity}",
            cancellationToken);
        return rowsAffected > 0;
    }

    public async Task RestockAsync(int variantId, int quantity, CancellationToken cancellationToken = default)
    {
        if (_isInMemory)
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken);
            if (variant is null)
            {
                return;
            }
            variant.Stock += quantity;
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ProductVariants\" SET \"Stock\" = \"Stock\" + {quantity} WHERE \"Id\" = {variantId}",
            cancellationToken);
    }
}
