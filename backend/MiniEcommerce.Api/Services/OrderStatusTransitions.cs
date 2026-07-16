using System.Collections.Frozen;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Services;

/// <summary>
/// Pure-function state machine for <see cref="OrderStatus"/> transitions.
///
/// Allowed transitions:
///   Pending  → { Paid, Cancelled }
///   Paid     → { Shipped, Cancelled }
///   Shipped  → { Delivered, Cancelled }
///   Delivered → ∅ (terminal)
///   Cancelled → ∅ (terminal)
/// </summary>
public static class OrderStatusTransitions
{
    private static readonly FrozenDictionary<OrderStatus, FrozenSet<OrderStatus>> Allowed =
        new Dictionary<OrderStatus, FrozenSet<OrderStatus>>
        {
            [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled }.ToFrozenSet(),
            [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled }.ToFrozenSet(),
            [OrderStatus.Shipped] = new[] { OrderStatus.Delivered, OrderStatus.Cancelled }.ToFrozenSet(),
            [OrderStatus.Delivered] = FrozenSet<OrderStatus>.Empty,
            [OrderStatus.Cancelled] = FrozenSet<OrderStatus>.Empty,
        }.ToFrozenDictionary();

    /// <summary>
    /// Returns the set of statuses that <paramref name="current"/> may
    /// transition to. Returns an empty set for terminal states.
    /// </summary>
    public static IReadOnlySet<OrderStatus> AllowedNexts(OrderStatus current)
        => Allowed.TryGetValue(current, out var next) ? next : FrozenSet<OrderStatus>.Empty;
}
