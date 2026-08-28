using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;


namespace EnterpriseCommerce.Domain.Payments;

public sealed class PaymentAttempt : AggregateRoot<PaymentAttemptId>
{
    public OrderId OrderId { get; private set; }
    public Money Amount { get; private set; }
    public string Provider { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public PaymentAttemptStatus Status { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private PaymentAttempt(
        PaymentAttemptId id,
        OrderId orderId,
        Money amount,
        string provider,
        Guid idempotencyKey,
        DateTimeOffset createdAt)
        : base(id)
    {
        OrderId = orderId;
        Amount = amount;
        Provider = provider;
        IdempotencyKey = idempotencyKey;
        Status = PaymentAttemptStatus.Pending;
        CreatedAt = createdAt;
    }

    private PaymentAttempt()
    {
        OrderId = null!;
        Amount = null!;
        Provider = null!;
    }

    public static PaymentAttempt Create(
        OrderId orderId,
        Money amount,
        string provider,
        Guid idempotencyKey,
        DateTimeOffset createdAt)
    {
        return new PaymentAttempt(
            new PaymentAttemptId(Guid.NewGuid()),
            orderId,
            amount,
            provider,
            idempotencyKey,
            createdAt);
    }

    public Result MarkAsSucceeded(string providerTransactionId, DateTimeOffset completedAt)
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            return Result.Failure(PaymentErrors.InvalidStatusTransition);
        }

        ProviderTransactionId = providerTransactionId;
        Status = PaymentAttemptStatus.Succeeded;
        CompletedAt = completedAt;

        return Result.Success();
    }

    public Result MarkAsFailed(string? providerTransactionId, DateTimeOffset completedAt)
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            return Result.Failure(PaymentErrors.InvalidStatusTransition);
        }

        ProviderTransactionId = providerTransactionId;
        Status = PaymentAttemptStatus.Failed;
        CompletedAt = completedAt;

        return Result.Success();
    }

    public Result MarkAsRefundRequired(string providerTransactionId, DateTimeOffset completedAt)
    {
        if (Status != PaymentAttemptStatus.Pending)
        {
            return Result.Failure(PaymentErrors.InvalidStatusTransition);
        }

        ProviderTransactionId = providerTransactionId;
        Status = PaymentAttemptStatus.RefundRequired;
        CompletedAt = completedAt;

        return Result.Success();
    }
}
