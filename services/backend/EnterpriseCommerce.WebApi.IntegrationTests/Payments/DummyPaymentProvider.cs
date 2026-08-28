using System;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseCommerce.Application.Payments;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

public class DummyPaymentProvider : IPaymentProvider
{
    public Task<InitiatePaymentResponse> InitiatePaymentAsync(EnterpriseCommerce.Domain.Payments.ValueObjects.PaymentAttemptId paymentAttemptId, EnterpriseCommerce.Domain.Orders.ValueObjects.OrderId orderId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        // Must be deterministic/idempotent for testing idempotency!
        return Task.FromResult(new InitiatePaymentResponse($"dummy_txn_{paymentAttemptId.Value}", "http://dummy.url"));
    }
}
