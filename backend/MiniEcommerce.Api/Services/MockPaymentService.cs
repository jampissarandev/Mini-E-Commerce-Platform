using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Interfaces;

namespace MiniEcommerce.Api.Services;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing mock payment for Order {OrderId}, Amount {Amount} {Currency}",
            request.OrderId, request.Amount, request.Currency);

        // Simulate payment processing delay
        await Task.Delay(200, cancellationToken);

        return new PaymentResult
        {
            Success = true,
            TransactionId = $"mock-{Guid.NewGuid()}",
            Message = "Mock payment processed successfully.",
            Status = PaymentStatus.Succeeded
        };
    }
}
