using EnterpriseCommerce.Domain.Payments;

namespace EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;

public sealed record AdminRefundPaymentResult(
    Guid PaymentAttemptId,
    AdminRefundOutcome Outcome,
    PaymentRefundStatus? RefundStatus);
