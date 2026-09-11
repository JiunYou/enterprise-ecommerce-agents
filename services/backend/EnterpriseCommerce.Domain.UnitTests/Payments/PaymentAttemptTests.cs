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
    [Fact]
    public void MarkAsSucceeded_WithValidReference_PersistsNormalizedReference()
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, "AUTH123");

        attempt.ProviderAuthorizationReference.Should().Be("AUTH123");
    }

    [Fact]
    public void MarkAsSucceeded_WithReferenceContainingWhitespace_PreservesExactOriginalValue()
    {
        var attempt = CreatePendingAttempt();
        const string refWithSpaces = "  AUTH123  ";
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, refWithSpaces);

        attempt.ProviderAuthorizationReference.Should().Be(refWithSpaces);
    }

    [Fact]
    public void MarkAsRefundRequired_WithValidReference_PersistsNormalizedReference()
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsRefundRequired("tx-123", DateTimeOffset.UtcNow, "AUTH456");

        attempt.ProviderAuthorizationReference.Should().Be("AUTH456");
    }

    [Fact]
    public void MarkAsSucceeded_WithNullReference_LeavesNull()
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, null);

        attempt.ProviderAuthorizationReference.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsSucceeded_WithBlankOrWhitespaceReference_StoresNull(string badRef)
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, badRef);

        attempt.ProviderAuthorizationReference.Should().BeNull();
    }

    [Fact]
    public void MarkAsSucceeded_WithReferenceExceeding100Chars_StoresNull()
    {
        var attempt = CreatePendingAttempt();
        var longRef = new string('X', 101);
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, longRef);

        attempt.ProviderAuthorizationReference.Should().BeNull();
    }

    [Fact]
    public void MarkAsSucceeded_WithReferenceExactly100Chars_Persists()
    {
        var attempt = CreatePendingAttempt();
        var ref100 = new string('A', 100);
        attempt.MarkAsSucceeded("tx-123", DateTimeOffset.UtcNow, ref100);

        attempt.ProviderAuthorizationReference.Should().Be(ref100);
    }

    [Fact]
    public void MarkAsRefundRequired_WithNullReference_LeavesNull()
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsRefundRequired("tx-123", DateTimeOffset.UtcNow, null);

        attempt.ProviderAuthorizationReference.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsRefundRequired_WithBlankOrWhitespaceReference_StoresNull(string badRef)
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsRefundRequired("tx-123", DateTimeOffset.UtcNow, badRef);

        attempt.ProviderAuthorizationReference.Should().BeNull();
    }

    [Fact]
    public void RequireRefundAfterCancellation_WhenSucceeded_ShouldTransitionToRefundRequiredAndPreserveMetadata()
    {
        var attempt = CreatePendingAttempt();
        var txId = "tx-paid-cancel-123";
        var completedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var authRef = "AUTH-PRESERVE-789";

        var succeedResult = attempt.MarkAsSucceeded(txId, completedAt, authRef);
        succeedResult.IsSuccess.Should().BeTrue();

        var expectedId = attempt.Id;
        var expectedOrderId = attempt.OrderId;
        var expectedAmount = attempt.Amount.Amount;
        var expectedCurrency = attempt.Amount.Currency;
        var expectedProvider = attempt.Provider;
        var expectedTxId = attempt.ProviderTransactionId;
        var expectedAuthRef = attempt.ProviderAuthorizationReference;
        var expectedCreatedAt = attempt.CreatedAt;
        var expectedCompletedAt = attempt.CompletedAt;
        var expectedIdempotencyKey = attempt.IdempotencyKey;

        var result = attempt.RequireRefundAfterCancellation();

        result.IsSuccess.Should().BeTrue();
        attempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired);

        attempt.Id.Should().Be(expectedId);
        attempt.OrderId.Should().Be(expectedOrderId);
        attempt.Amount.Amount.Should().Be(expectedAmount);
        attempt.Amount.Currency.Should().Be(expectedCurrency);
        attempt.Provider.Should().Be(expectedProvider);
        attempt.ProviderTransactionId.Should().Be(expectedTxId);
        attempt.ProviderAuthorizationReference.Should().Be(expectedAuthRef);
        attempt.CreatedAt.Should().Be(expectedCreatedAt);
        attempt.CompletedAt.Should().Be(expectedCompletedAt);
        attempt.IdempotencyKey.Should().Be(expectedIdempotencyKey);
    }

    [Theory]
    [InlineData(PaymentAttemptStatus.Pending)]
    [InlineData(PaymentAttemptStatus.Failed)]
    [InlineData(PaymentAttemptStatus.RefundRequired)]
    public void RequireRefundAfterCancellation_WhenNotSucceeded_ShouldFailWithoutMutation(PaymentAttemptStatus nonSucceededState)
    {
        var attempt = CreatePendingAttempt();
        var time = DateTimeOffset.UtcNow;

        switch (nonSucceededState)
        {
            case PaymentAttemptStatus.Pending:
                break;
            case PaymentAttemptStatus.Failed:
                attempt.MarkAsFailed("tx-fail", time);
                break;
            case PaymentAttemptStatus.RefundRequired:
                attempt.MarkAsRefundRequired("tx-refund", time);
                break;
        }

        var statusBefore = attempt.Status;
        var txBefore = attempt.ProviderTransactionId;
        var completedBefore = attempt.CompletedAt;

        var result = attempt.RequireRefundAfterCancellation();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidStatusTransition);
        attempt.Status.Should().Be(statusBefore);
        attempt.ProviderTransactionId.Should().Be(txBefore);
        attempt.CompletedAt.Should().Be(completedBefore);
    }

    [Fact]
    public void MarkAsRefundRequired_FromSucceeded_ShouldFailAndNotBeBroadened()
    {
        var attempt = CreatePendingAttempt();
        attempt.MarkAsSucceeded("tx-succeeded", DateTimeOffset.UtcNow);

        var result = attempt.MarkAsRefundRequired("tx-new", DateTimeOffset.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidStatusTransition);
        attempt.Status.Should().Be(PaymentAttemptStatus.Succeeded);
    }
}
