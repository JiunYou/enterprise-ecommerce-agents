using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments;

public record InitiatePaymentResponse(string ProviderTransactionId, string ClientSecretUrl);

public interface IPaymentProvider
{
    Task<InitiatePaymentResponse> InitiatePaymentAsync(
        PaymentAttemptId paymentAttemptId,
        OrderId orderId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);
}
