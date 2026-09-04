using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments;

public static class PaymentLaunchMethod
{
    public const string Get = "GET";
    public const string Post = "POST";
}

public sealed record InitiatePaymentResponse(
    string? ProviderTransactionId,
    string ActionUrl,
    string Method = PaymentLaunchMethod.Get,
    IReadOnlyDictionary<string, string>? FormFields = null);

public interface IPaymentProvider
{
    string ProviderName { get; }

    Task<InitiatePaymentResponse> InitiatePaymentAsync(
        PaymentAttemptId paymentAttemptId,
        OrderId orderId,
        decimal amount,
        string currency,
        DateTimeOffset paymentAttemptCreatedAt,
        CancellationToken cancellationToken = default);
}
