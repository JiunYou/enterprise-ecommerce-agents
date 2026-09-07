using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using System;

namespace EnterpriseCommerce.Domain.Payments
{
    public sealed class PaymentRefund : AggregateRoot<PaymentAttemptId>
    {
        public PaymentRefundStatus Status { get; private set; }
        public string Reason { get; private set; } = null!;
        public string ActorIssuer { get; private set; } = null!;
        public string ActorSubject { get; private set; } = null!;
        public DateTimeOffset RequestedAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }

        // EF Core constructor
        private PaymentRefund() { }

        private PaymentRefund(PaymentAttemptId id,
                               string reason,
                               string actorIssuer,
                               string actorSubject,
                               DateTimeOffset requestedAt)
            : base(id)
        {
            Reason = reason;
            ActorIssuer = actorIssuer;
            ActorSubject = actorSubject;
            RequestedAt = requestedAt;
            Status = PaymentRefundStatus.Pending;
            CompletedAt = null;
        }

        public static Result<PaymentRefund> Create(PaymentAttemptId paymentAttemptId,
                                                   string reason,
                                                   string actorIssuer,
                                                   string actorSubject,
                                                   DateTimeOffset requestedAt)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure<PaymentRefund>(PaymentErrors.InvalidRefundReason);
            reason = reason.Trim();
            if (reason.Length > 500)
                return Result.Failure<PaymentRefund>(PaymentErrors.InvalidRefundReason);

            if (string.IsNullOrWhiteSpace(actorIssuer) || actorIssuer.Length > 512)
                return Result.Failure<PaymentRefund>(PaymentErrors.InvalidActorIssuer);

            if (string.IsNullOrWhiteSpace(actorSubject) || actorSubject.Length > 255)
                return Result.Failure<PaymentRefund>(PaymentErrors.InvalidActorSubject);

            var refund = new PaymentRefund(paymentAttemptId, reason, actorIssuer, actorSubject, requestedAt);
            return Result.Success<PaymentRefund>(refund);
        }

        public Result MarkAsSucceeded(DateTimeOffset completedAt)
        {
            if (Status != PaymentRefundStatus.Pending && Status != PaymentRefundStatus.Unresolved)
                return Result.Failure(PaymentErrors.InvalidRefundStatusTransition);

            Status = PaymentRefundStatus.Succeeded;
            CompletedAt = completedAt;
            return Result.Success();
        }

        public Result MarkAsFailed(DateTimeOffset completedAt)
        {
            if (Status != PaymentRefundStatus.Pending)
                return Result.Failure(PaymentErrors.InvalidRefundStatusTransition);

            Status = PaymentRefundStatus.Failed;
            CompletedAt = completedAt;
            return Result.Success();
        }

        public Result MarkAsUnresolved()
        {
            if (Status != PaymentRefundStatus.Pending)
                return Result.Failure(PaymentErrors.InvalidRefundStatusTransition);

            Status = PaymentRefundStatus.Unresolved;
            // CompletedAt remains null.
            return Result.Success();
        }
    }
}
