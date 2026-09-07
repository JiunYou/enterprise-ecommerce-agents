using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments;

public interface IPaymentRefundRepository
{
    Task<PaymentRefund?> GetByPaymentAttemptIdAsync(PaymentAttemptId paymentAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentRefund>> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default);

    Task<bool> TryCreateIntentAsync(PaymentRefund refund, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
