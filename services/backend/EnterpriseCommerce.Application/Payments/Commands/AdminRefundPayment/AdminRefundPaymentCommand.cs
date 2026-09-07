using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;

public sealed record AdminRefundPaymentCommand(
    Guid PaymentAttemptId,
    string ActorIssuer,
    string ActorSubject,
    string? Reason) : ICommand<AdminRefundPaymentResult>;
