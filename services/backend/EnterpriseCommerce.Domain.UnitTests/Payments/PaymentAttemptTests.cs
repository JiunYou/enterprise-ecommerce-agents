using System;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using FluentAssertions;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Payments;

public class PaymentAttemptTests
{
    private PaymentAttempt CreatePendingAttempt()
    {
        return PaymentAttempt.Create(
            new OrderId(Guid.NewGuid()),
            new Money(100m, "USD"),
            "Dummy",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void MarkAsSucceeded_FromPending_TransitionsToSucceeded()
    {
        var attempt = CreatePendingAttempt();
        var txId = "tx-123";
        var time = DateTimeOffset.UtcNow;

        attempt.MarkAsSucceeded(txId, time);

        attempt.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        attempt.ProviderTransactionId.Should().Be(txId);
        attempt.CompletedAt.Should().Be(time);
    }

    [Fact]
    public void MarkAsFailed_FromPending_TransitionsToFailed()
    {
        var attempt = CreatePendingAttempt();
        var txId = "tx-123";
        var time = DateTimeOffset.UtcNow;

        attempt.MarkAsFailed(txId, time);

        attempt.Status.Should().Be(PaymentAttemptStatus.Failed);
        attempt.ProviderTransactionId.Should().Be(txId);
        attempt.CompletedAt.Should().Be(time);
    }

    [Fact]
    public void MarkAsRefundRequired_FromPending_TransitionsToRefundRequired()
    {
        var attempt = CreatePendingAttempt();
        var txId = "tx-123";
        var time = DateTimeOffset.UtcNow;

        attempt.MarkAsRefundRequired(txId, time);

        attempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired);
        attempt.ProviderTransactionId.Should().Be(txId);
        attempt.CompletedAt.Should().Be(time);
    }

    [Theory]
    [InlineData(PaymentAttemptStatus.Succeeded)]
    [InlineData(PaymentAttemptStatus.Failed)]
    [InlineData(PaymentAttemptStatus.RefundRequired)]
    public void MarkAsSucceeded_FromTerminalState_ThrowsException(PaymentAttemptStatus terminalState)
    {
        var attempt = CreatePendingAttempt();
        var time = DateTimeOffset.UtcNow;

        // Force transition to terminal
        switch (terminalState)
        {
            case PaymentAttemptStatus.Succeeded: attempt.MarkAsSucceeded("tx", time); break;
            case PaymentAttemptStatus.Failed: attempt.MarkAsFailed("tx", time); break;
            case PaymentAttemptStatus.RefundRequired: attempt.MarkAsRefundRequired("tx", time); break;
        }

        var result = attempt.MarkAsSucceeded("tx2", time);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidStatusTransition);
    }
}
