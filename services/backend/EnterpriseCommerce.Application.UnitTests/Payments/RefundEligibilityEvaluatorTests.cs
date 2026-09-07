using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Payments;

public class RefundEligibilityEvaluatorTests
{
    private readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private const int DefaultExpirationMinutes = 15;

    private static Order CreateOrder(OrderStatus status, DateTimeOffset? submittedAt = null)
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);

        if (status == OrderStatus.Pending)
        {
            return order;
        }

        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "City", "Line1", null).Value;
        order.Submit(address, submittedAt ?? DateTimeOffset.UtcNow);

        if (status == OrderStatus.Submitted)
        {
            return order;
        }

        if (status == OrderStatus.Paid)
        {
            order.ChangeStatus(OrderStatus.Paid);
            return order;
        }

        if (status == OrderStatus.Shipped)
        {
            order.ChangeStatus(OrderStatus.Paid);
            order.ChangeStatus(OrderStatus.Shipped);
            return order;
        }

        if (status == OrderStatus.Cancelled)
        {
            order.Cancel();
            return order;
        }

        return order;
    }

    private static PaymentAttempt CreateAttempt(
        OrderId orderId,
        PaymentAttemptStatus status,
        string? authRef = "AUTH_REF_123",
        string? providerTxId = "TX_123")
    {
        var attempt = PaymentAttempt.Create(
            orderId,
            new Money(100m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            authRef);

        if (status == PaymentAttemptStatus.RefundRequired)
        {
            attempt.MarkAsRefundRequired(providerTxId ?? "TX_123", DateTimeOffset.UtcNow, authRef);
        }
        else if (status == PaymentAttemptStatus.Succeeded)
        {
            attempt.MarkAsSucceeded(providerTxId ?? "TX_123", DateTimeOffset.UtcNow, authRef);
        }
        else if (status == PaymentAttemptStatus.Failed)
        {
            attempt.MarkAsFailed(providerTxId, DateTimeOffset.UtcNow);
        }

        return attempt;
    }

    [Fact]
    public void Evaluate_RefundRequiredAndCancelled_ReturnsEligible()
    {
        var order = CreateOrder(OrderStatus.Cancelled);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.Eligible, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndExpiredSubmitted_ReturnsEligible()
    {
        var submittedAt = _now.AddMinutes(-16); // 16 min ago > 15 min threshold
        var order = CreateOrder(OrderStatus.Submitted, submittedAt);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.Eligible, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndNonExpiredSubmitted_ReturnsNotEligible()
    {
        var submittedAt = _now.AddMinutes(-10); // 10 min ago < 15 min threshold
        var order = CreateOrder(OrderStatus.Submitted, submittedAt);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.NotEligible, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndPaidWithAnotherSucceeded_ReturnsEligible()
    {
        var order = CreateOrder(OrderStatus.Paid);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");
        var otherSucceeded = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "AUTH_999");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target, otherSucceeded], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.Eligible, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndShippedWithAnotherSucceeded_ReturnsEligible()
    {
        var order = CreateOrder(OrderStatus.Shipped);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");
        var otherSucceeded = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "AUTH_999");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target, otherSucceeded], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.Eligible, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndPaidWithoutAnotherSucceeded_ReturnsInvariantViolation()
    {
        var order = CreateOrder(OrderStatus.Paid);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");
        var otherFailed = CreateAttempt(order.Id, PaymentAttemptStatus.Failed);

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target, otherFailed], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.InvariantViolation, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndShippedWithoutAnotherSucceeded_ReturnsInvariantViolation()
    {
        var order = CreateOrder(OrderStatus.Shipped);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.InvariantViolation, result);
    }

    [Fact]
    public void Evaluate_RefundRequiredAndPendingOrder_ReturnsInvariantViolation()
    {
        var order = CreateOrder(OrderStatus.Pending);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.InvariantViolation, result);
    }

    [Fact]
    public void Evaluate_TargetPending_ReturnsNotRefundRequired()
    {
        var order = CreateOrder(OrderStatus.Cancelled);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.Pending, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.NotRefundRequired, result);
    }

    [Fact]
    public void Evaluate_TargetSucceeded_ReturnsNotRefundRequired()
    {
        var order = CreateOrder(OrderStatus.Cancelled);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "AUTH_123");

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.NotRefundRequired, result);
    }

    [Fact]
    public void Evaluate_TargetFailed_ReturnsNotRefundRequired()
    {
        var order = CreateOrder(OrderStatus.Cancelled);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.Failed);

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.NotRefundRequired, result);
    }

    [Fact]
    public void Evaluate_MissingProviderAuthorizationReference_ReturnsManualProviderResolutionRequired()
    {
        var order = CreateOrder(OrderStatus.Cancelled);
        var target = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, authRef: null);

        var result = RefundEligibilityEvaluator.Evaluate(target, order, [target], _now, DefaultExpirationMinutes);

        Assert.Equal(RefundEligibilityStatus.ManualProviderResolutionRequired, result);
    }
}
