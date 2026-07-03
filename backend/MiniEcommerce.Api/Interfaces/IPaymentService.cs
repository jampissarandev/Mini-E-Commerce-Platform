using MiniEcommerce.Api.Dtos;

namespace MiniEcommerce.Api.Interfaces;

public interface IPaymentService
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken cancellationToken = default);
}
