namespace MiniEcommerce.Api.Interfaces;

/// <summary>
/// Atomic variant stock mutation. The single seam that decides whether to
/// issue a raw SQL <c>UPDATE ... WHERE Stock &gt;= @qty</c> (Postgres) or a
/// tracked-entity read-then-decrement (EF Core InMemory).
///
/// <para>
/// The Postgres path is the real concurrency story (ADR 0002): the atomic
/// guard rejects a second concurrent deduction for the same in-stock row.
/// The InMemory path exists so unit/integration tests can drive the full
/// checkout flow without spinning up Postgres; the InMemory provider ignores
/// indexes and is single-threaded by construction, so the guard is safe.
/// </para>
/// </summary>
public interface IVariantStockWriter
{
    /// <summary>
    /// Attempt to deduct <paramref name="quantity"/> units from variant
    /// <paramref name="variantId"/>. Returns <c>true</c> if the deduction
    /// committed, <c>false</c> if the variant doesn't exist or has
    /// insufficient stock. Atomic — never partial.
    /// </summary>
    Task<bool> TryDeductAsync(int variantId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add <paramref name="quantity"/> units back to variant
    /// <paramref name="variantId"/>. Used by checkout to roll back already-deducted
    /// items when a later item fails (INSUFFICIENT_STOCK) or when payment
    /// fails (PAYMENT_FAILED). No-op if the variant doesn't exist.
    /// </summary>
    Task RestockAsync(int variantId, int quantity, CancellationToken cancellationToken = default);
}
