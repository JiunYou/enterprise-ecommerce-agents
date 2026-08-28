using EnterpriseCommerce.Domain.Payments;

namespace EnterpriseCommerce.Application.Payments;

public interface IPaymentWebhookReceiptRepository
{
    Task<bool> ExistsAsync(string provider, string providerEventId, CancellationToken cancellationToken = default);
    void Add(PaymentWebhookReceipt receipt);
}
