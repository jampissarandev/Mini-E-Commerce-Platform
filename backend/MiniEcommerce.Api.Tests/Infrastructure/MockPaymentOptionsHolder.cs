using MiniEcommerce.Api.Services;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Thread-safe mutable holder for <see cref="MockPaymentOptions"/>. Registered
/// as a singleton in the test host so individual tests can flip the
/// failure-injection mode at runtime without rebuilding the service provider.
/// </summary>
public sealed class MockPaymentOptionsHolder
{
    private MockPaymentOptions _current = new();

    private readonly object _gate = new();

    public MockPaymentOptions Snapshot()
    {
        lock (_gate) return _current;
    }

    public void Set(MockPaymentOptions options)
    {
        lock (_gate) _current = options;
    }
}
