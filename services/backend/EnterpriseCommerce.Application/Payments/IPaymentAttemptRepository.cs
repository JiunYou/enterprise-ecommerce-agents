using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments;

public interface IPaymentAttemptRepository
{
    Task<PaymentAttempt?> GetByIdAsync(PaymentAttemptId id, CancellationToken cancellationToken = default);
    Task<PaymentAttempt?> GetActivePendingAttemptAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task<PaymentAttempt?> GetByProviderTransactionIdAsync(string provider, string providerTransactionId, CancellationToken cancellationToken = default);
    void Add(PaymentAttempt paymentAttempt);
}
