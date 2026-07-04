using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Interfaces;

namespace MiniEcommerce.Api.Services;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;
    private readonly MockPaymentOptions _options;

    public MockPaymentService(
        IOptions<MockPaymentOptions> options,
        ILogger<MockPaymentService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing mock payment for Order {OrderId}, Amount {Amount} {Currency}, Mode {Mode}",
            request.OrderId, request.Amount, request.Currency, _options.Mode);

        // Simulate payment processing delay
        await Task.Delay(200, cancellationToken);

        return _options.Mode switch
        {
            MockPaymentMode.AlwaysFail => new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Failed,
                Message = "Mock payment was forced to fail (Payments:Mock:Mode = AlwaysFail).",
            },

            MockPaymentMode.FailIfAmountGreaterThan when request.Amount > _options.FailIfAmountGreaterThan
                => new PaymentResult
                {
                    Success = false,
                    Status = PaymentStatus.Failed,
                    Message = $"Mock payment declined: amount {request.Amount} {request.Currency} exceeds the configured failure threshold of {_options.FailIfAmountGreaterThan}.",
                },

            _ => new PaymentResult
            {
                Success = true,
                TransactionId = $"mock-{Guid.NewGuid()}",
                Message = "Mock payment processed successfully.",
                Status = PaymentStatus.Succeeded,
            },
        };
    }
}
