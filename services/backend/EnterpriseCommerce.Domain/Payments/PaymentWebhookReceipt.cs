using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Payments;

public sealed class PaymentWebhookReceipt : Entity<Guid>
{
    public string Provider { get; private set; }
    public string ProviderEventId { get; private set; }
    public PaymentAttemptId? PaymentAttemptId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    private PaymentWebhookReceipt(
        Guid id,
        string provider,
        string providerEventId,
        PaymentAttemptId? paymentAttemptId,
        DateTimeOffset receivedAt)
        : base(id)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        PaymentAttemptId = paymentAttemptId;
        ReceivedAt = receivedAt;
    }

    private PaymentWebhookReceipt()
    {
        Provider = null!;
        ProviderEventId = null!;
    }

    public static PaymentWebhookReceipt Create(
        string provider,
        string providerEventId,
        PaymentAttemptId? paymentAttemptId,
        DateTimeOffset receivedAt)
    {
        return new PaymentWebhookReceipt(
            Guid.NewGuid(),
            provider,
            providerEventId,
            paymentAttemptId,
            receivedAt);
    }
}
