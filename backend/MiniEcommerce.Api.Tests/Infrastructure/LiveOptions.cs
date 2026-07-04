using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Services;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Infrastructure;

/// <summary>
/// Live <see cref="IOptions{TOptions}"/> wrapper that re-reads its value from a
/// <see cref="MockPaymentOptionsHolder"/> on every access. Used in the test
/// host so <c>PaymentsController</c> and <c>MockPaymentService</c> see the
/// current mode after <c>SetPaymentMode(...)</c> is called, without rebuilding
/// the service provider.
/// </summary>
internal sealed class LiveMockPaymentOptions : IOptions<MockPaymentOptions>
{
    private readonly MockPaymentOptionsHolder _holder;

    public LiveMockPaymentOptions(MockPaymentOptionsHolder holder) => _holder = holder;

    public MockPaymentOptions Value => _holder.Snapshot();
}
