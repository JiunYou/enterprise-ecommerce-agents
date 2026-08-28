using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;

public sealed record ProcessPaymentWebhookCommand(
    Guid PaymentAttemptId,
    string Provider,
    string ProviderEventId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    bool IsSuccess) : ICommand;
