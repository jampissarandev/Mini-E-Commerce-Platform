namespace MiniEcommerce.Api.Services;

/// <summary>
/// Controls the behaviour of <see cref="MockPaymentService"/> so demos and
/// integration tests can exercise the failure path without swapping in a real
/// payment gateway. Bound from the <c>Payments:Mock</c> configuration section.
/// </summary>
public class MockPaymentOptions
{
    public const string SectionName = "Payments:Mock";

    /// <summary>
    /// Failure-injection mode. Defaults to <see cref="MockPaymentMode.AlwaysSucceed"/>
    /// so production checkout never accidentally fails.
    /// </summary>
    public MockPaymentMode Mode { get; set; } = MockPaymentMode.AlwaysSucceed;

    /// <summary>
    /// Threshold (inclusive) used by <see cref="MockPaymentMode.FailIfAmountGreaterThan"/>.
    /// Amounts strictly greater than this value will fail.
    /// </summary>
    public decimal FailIfAmountGreaterThan { get; set; } = 1000m;
}

public enum MockPaymentMode
{
    /// <summary>Default — every charge succeeds (backward compatible).</summary>
    AlwaysSucceed = 0,

    /// <summary>Every charge fails. Useful for demonstrating the failure UI.</summary>
    AlwaysFail = 1,

    /// <summary>
    /// Charges succeed unless <c>Amount</c> strictly exceeds
    /// <see cref="MockPaymentOptions.FailIfAmountGreaterThan"/>. Useful for demos
    /// where you want a single high-priced product to trigger the failure.
    /// </summary>
    FailIfAmountGreaterThan = 2,
}
