using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Unit.Services;

/// <summary>
/// TDD-driven characterization + failure-injection tests for
/// <see cref="MockPaymentService"/>. The service is the only
/// <c>IPaymentService</c> implementation registered in production; these tests
/// pin its three behaviours:
///   1. Default mode always succeeds (preserves backward compatibility).
///   2. <c>Fail</c> mode always returns Success=false with a deterministic code.
///   3. <c>FailIfAmountGreaterThan</c> mode fails only when the amount exceeds
///      a configured threshold (lets demos trigger the failure path with a
///      single high-priced product).
/// </summary>
public class MockPaymentServiceTests
{
    private static MockPaymentService NewSut(MockPaymentOptions? options = null)
    {
        var opts = Options.Create(options ?? new MockPaymentOptions());
        return new MockPaymentService(opts, NullLogger<MockPaymentService>.Instance);
    }

    private static PaymentRequest NewRequest(decimal amount = 29.99m) => new()
    {
        Amount = amount,
        Currency = "USD",
        OrderId = Guid.NewGuid(),
    };

    [Fact]
    public async Task ChargeAsync_DefaultMode_AlwaysSucceeds()
    {
        var sut = NewSut();

        var result = await sut.ChargeAsync(NewRequest());

        result.Success.Should().BeTrue();
        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.TransactionId.Should().StartWith("mock-");
    }

    [Fact]
    public async Task ChargeAsync_AlwaysFailMode_ReturnsFailure()
    {
        var sut = NewSut(new MockPaymentOptions { Mode = MockPaymentMode.AlwaysFail });

        var result = await sut.ChargeAsync(NewRequest());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.Message.Should().NotBeNullOrEmpty();
        result.TransactionId.Should().BeNull();
    }

    [Fact]
    public async Task ChargeAsync_FailIfAmountGreaterThan_UnderThreshold_Succeeds()
    {
        var sut = NewSut(new MockPaymentOptions
        {
            Mode = MockPaymentMode.FailIfAmountGreaterThan,
            FailIfAmountGreaterThan = 100m,
        });

        var result = await sut.ChargeAsync(NewRequest(amount: 50m));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ChargeAsync_FailIfAmountGreaterThan_OverThreshold_Fails()
    {
        var sut = NewSut(new MockPaymentOptions
        {
            Mode = MockPaymentMode.FailIfAmountGreaterThan,
            FailIfAmountGreaterThan = 100m,
        });

        var result = await sut.ChargeAsync(NewRequest(amount: 150m));

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.Message.Should().Contain("150");
    }
}
