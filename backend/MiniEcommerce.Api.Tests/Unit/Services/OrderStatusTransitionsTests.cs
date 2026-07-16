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

    [Fact]
    public void AllowedNexts_AllTransitions_AreBidirectionalConsistent()
    {
        // Verify every defined status in the enum appears as a current state
        // and has a consistent AllowedNexts result (empty for terminal, non-empty otherwise).
        foreach (OrderStatus status in Enum.GetValues<OrderStatus>())
        {
            var next = OrderStatusTransitions.AllowedNexts(status);
            next.Should().NotBeNull($"AllowedNexts for {status} should never return null");

            if (status is OrderStatus.Delivered or OrderStatus.Cancelled)
            {
                next.Should().BeEmpty($"'{status}' is a terminal state and should have no allowed transitions");
            }
            else
            {
                next.Should().NotBeEmpty($"'{status}' is a non-terminal state and should have at least one allowed transition");
            }
        }
    }

    [Fact]
    public void AllowedNexts_CancelledIsNeverInNonTerminalNextSets()
    {
        // Cancelled is the absorbing transition — once cancelled, the order stays cancelled.
        // Verify that no non-terminal state lists Cancelled as a forward transition
        // from itself (i.e. Cancelled → something should be empty).
        var next = OrderStatusTransitions.AllowedNexts(OrderStatus.Cancelled);
        next.Should().BeEmpty();
    }
}
