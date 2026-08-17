using FluentAssertions;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// Pure-function unit tests for <see cref="OrderStatusTransitions.AllowedNexts"/>.
/// These tests never touch a database or HTTP — they simply verify the state
/// machine table defined in the helper class.
///
/// Expected transitions (7 cases):
///   Pending   → { Paid, Cancelled }          (2 next states)
///   Paid      → { Shipped, Cancelled }        (2 next states)
///   Shipped   → { Delivered, Cancelled }      (2 next states)
///   Delivered → ∅                             (terminal — empty set)
///   Cancelled → ∅                             (terminal — empty set)
/// </summary>
public class OrderStatusTransitionsTests
{
    [Fact]
    public void AllowedNexts_Pending_ReturnsPaidAndCancelled()
    {
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Pending);

        next.Should().HaveCount(2);
        next.Should().Contain(OrderStatus.Paid);
        next.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void AllowedNexts_Paid_ReturnsShippedAndCancelled()
    {
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Paid);

        next.Should().HaveCount(2);
        next.Should().Contain(OrderStatus.Shipped);
        next.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void AllowedNexts_Shipped_ReturnsDeliveredAndCancelled()
    {
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Shipped);

        next.Should().HaveCount(2);
        next.Should().Contain(OrderStatus.Delivered);
        next.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void AllowedNexts_Delivered_ReturnsEmpty()
    {
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Delivered);

        next.Should().BeEmpty();
    }

    [Fact]
    public void AllowedNexts_Cancelled_ReturnsEmpty()
    {
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Cancelled);

        next.Should().BeEmpty();
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void CanTransition_Delivered_To_Anything_Returns_False(OrderStatus terminal)
    {
        // Terminal states have an empty AllowedNexts set, so CanTransition
        // must return false for any target. Parameterized over both terminal
        // values so the property is enforced once per terminal.
        foreach (var to in Enum.GetValues<OrderStatus>())
        {
            OrderStatusTransitions.CanTransition(terminal, to).Should().BeFalse(
                $"'{terminal}' is terminal and cannot transition to '{to}'");
        }
    }

    [Fact]
    public void CanTransition_Cancelled_To_Anything_Returns_False()
    {
        // Spec test case 7: Cancelled is the absorbing state. Spot-check
        // a representative subset of transitions — the parameterized
        // Terminal→Anything test above already covers the full enum sweep;
        // this case exists to give Cancelled a discoverable named test.
        OrderStatusTransitions.CanTransition(OrderStatus.Cancelled, OrderStatus.Pending).Should().BeFalse();
        OrderStatusTransitions.CanTransition(OrderStatus.Cancelled, OrderStatus.Paid).Should().BeFalse();
        OrderStatusTransitions.CanTransition(OrderStatus.Cancelled, OrderStatus.Shipped).Should().BeFalse();
        OrderStatusTransitions.CanTransition(OrderStatus.Cancelled, OrderStatus.Delivered).Should().BeFalse();
        OrderStatusTransitions.CanTransition(OrderStatus.Cancelled, OrderStatus.Cancelled).Should().BeFalse();
    }
}
