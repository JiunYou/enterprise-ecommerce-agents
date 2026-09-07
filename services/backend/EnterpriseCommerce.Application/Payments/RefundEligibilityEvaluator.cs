using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Payments;

namespace EnterpriseCommerce.Application.Payments;

public enum RefundEligibilityStatus
{
    Eligible,
    NotRefundRequired,
    ManualProviderResolutionRequired,
    InvariantViolation,
    NotEligible
}

public static class RefundEligibilityEvaluator
{
    public static RefundEligibilityStatus Evaluate(
        PaymentAttempt targetAttempt,
        Order order,
        IReadOnlyCollection<PaymentAttempt> allAttemptsForOrder,
        DateTimeOffset utcNow,
        int expirationWindowMinutes = 15)
    {
        ArgumentNullException.ThrowIfNull(targetAttempt);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(allAttemptsForOrder);

        // 1. Authoritative Refund Target: Only target PaymentAttempt.Status == RefundRequired
        if (targetAttempt.Status != PaymentAttemptStatus.RefundRequired)
        {
            return RefundEligibilityStatus.NotRefundRequired;
        }

        // 2. Missing ProviderAuthorizationReference requires manual resolution without calling provider
        if (string.IsNullOrWhiteSpace(targetAttempt.ProviderAuthorizationReference))
        {
            return RefundEligibilityStatus.ManualProviderResolutionRequired;
        }

        // 3. Local eligibility based on OrderStatus
        switch (order.Status)
        {
            case OrderStatus.Cancelled:
                return RefundEligibilityStatus.Eligible;

            case OrderStatus.Submitted:
                var window = Math.Max(0, expirationWindowMinutes);
                var threshold = utcNow.AddMinutes(-window);
                return order.IsExpired(threshold)
                    ? RefundEligibilityStatus.Eligible
                    : RefundEligibilityStatus.NotEligible;

            case OrderStatus.Paid:
            case OrderStatus.Shipped:
                bool hasAnotherSucceeded = allAttemptsForOrder.Any(a =>
                    a.Id != targetAttempt.Id &&
                    a.Status == PaymentAttemptStatus.Succeeded);

                return hasAnotherSucceeded
                    ? RefundEligibilityStatus.Eligible
                    : RefundEligibilityStatus.InvariantViolation;

            case OrderStatus.Pending:
                return RefundEligibilityStatus.InvariantViolation;

            default:
                return RefundEligibilityStatus.NotEligible;
        }
    }
}
