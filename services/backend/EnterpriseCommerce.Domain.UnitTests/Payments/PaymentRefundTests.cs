using System;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Payments;

public class PaymentRefundTests
{
    private static PaymentAttemptId NewAttemptId() => new(Guid.NewGuid());
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    // ── Creation ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParameters_Succeeds()
    {
        var result = PaymentRefund.Create(NewAttemptId(), "Customer request", "https://issuer.example", "sub-123", Now);

        result.IsSuccess.Should().BeTrue();
        var refund = result.Value;
        refund.Status.Should().Be(PaymentRefundStatus.Pending);
        refund.Reason.Should().Be("Customer request");
        refund.ActorIssuer.Should().Be("https://issuer.example");
        refund.ActorSubject.Should().Be("sub-123");
        refund.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_InitialVersion_IsZero()
    {
        var result = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now);
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(0u);
    }

    [Fact]
    public void Create_IdIsSuppliedPaymentAttemptId()
    {
        var attemptId = NewAttemptId();
        var result = PaymentRefund.Create(attemptId, "reason", "issuer", "subject", Now);
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(attemptId);
    }

    // ── Reason validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankReason_Fails(string blank)
    {
        var result = PaymentRefund.Create(NewAttemptId(), blank, "issuer", "subject", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundReason);
    }

    [Fact]
    public void Create_ReasonIsTrimmed()
    {
        var result = PaymentRefund.Create(NewAttemptId(), "  trimmed  ", "issuer", "subject", Now);
        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be("trimmed");
    }

    [Fact]
    public void Create_Reason500Chars_Succeeds()
    {
        var reason = new string('R', 500);
        var result = PaymentRefund.Create(NewAttemptId(), reason, "issuer", "subject", Now);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_Reason501Chars_Fails()
    {
        var reason = new string('R', 501);
        var result = PaymentRefund.Create(NewAttemptId(), reason, "issuer", "subject", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundReason);
    }

    // ── ActorIssuer validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankActorIssuer_Fails(string blank)
    {
        var result = PaymentRefund.Create(NewAttemptId(), "reason", blank, "subject", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidActorIssuer);
    }

    [Fact]
    public void Create_ActorIssuer512Chars_Succeeds()
    {
        var issuer = new string('I', 512);
        var result = PaymentRefund.Create(NewAttemptId(), "reason", issuer, "subject", Now);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_ActorIssuer513Chars_Fails()
    {
        var issuer = new string('I', 513);
        var result = PaymentRefund.Create(NewAttemptId(), "reason", issuer, "subject", Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidActorIssuer);
    }

    // ── ActorSubject validation ───────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankActorSubject_Fails(string blank)
    {
        var result = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", blank, Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidActorSubject);
    }

    [Fact]
    public void Create_ActorSubject255Chars_Succeeds()
    {
        var subject = new string('S', 255);
        var result = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", subject, Now);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_ActorSubject256Chars_Fails()
    {
        var subject = new string('S', 256);
        var result = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", subject, Now);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidActorSubject);
    }

    // ── Status transitions ────────────────────────────────────────────────────

    [Fact]
    public void MarkAsSucceeded_FromPending_Succeeds()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        var completedAt = Now;

        var result = refund.MarkAsSucceeded(completedAt);

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(PaymentRefundStatus.Succeeded);
        refund.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkAsFailed_FromPending_Succeeds()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        var completedAt = Now;

        var result = refund.MarkAsFailed(completedAt);

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(PaymentRefundStatus.Failed);
        refund.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkAsUnresolved_FromPending_Succeeds()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;

        var result = refund.MarkAsUnresolved();

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(PaymentRefundStatus.Unresolved);
        refund.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsSucceeded_FromUnresolved_Succeeds()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsUnresolved();
        var completedAt = Now;

        var result = refund.MarkAsSucceeded(completedAt);

        result.IsSuccess.Should().BeTrue();
        refund.Status.Should().Be(PaymentRefundStatus.Succeeded);
        refund.CompletedAt.Should().Be(completedAt);
    }

    [Fact]
    public void MarkAsSucceeded_WhenAlreadySucceeded_Fails()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsSucceeded(Now);

        var result = refund.MarkAsSucceeded(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundStatusTransition);
    }

    [Fact]
    public void MarkAsSucceeded_WhenFailed_Fails()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsFailed(Now);

        var result = refund.MarkAsSucceeded(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundStatusTransition);
    }

    [Fact]
    public void MarkAsFailed_WhenAlreadyFailed_Fails()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsFailed(Now);

        var result = refund.MarkAsFailed(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundStatusTransition);
    }

    [Fact]
    public void MarkAsFailed_WhenUnresolved_Fails()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsUnresolved();

        var result = refund.MarkAsFailed(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundStatusTransition);
    }

    [Fact]
    public void MarkAsUnresolved_WhenAlreadyUnresolved_Fails()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsUnresolved();

        var result = refund.MarkAsUnresolved();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRefundStatusTransition);
    }

    // ── CompletedAt semantics ─────────────────────────────────────────────────

    [Fact]
    public void InitialState_CompletedAt_IsNull()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void AfterMarkAsUnresolved_CompletedAt_RemainsNull()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        refund.MarkAsUnresolved();
        refund.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void AfterMarkAsSucceeded_CompletedAt_IsSet()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        var t = DateTimeOffset.UtcNow;
        refund.MarkAsSucceeded(t);
        refund.CompletedAt.Should().Be(t);
    }

    [Fact]
    public void AfterMarkAsFailed_CompletedAt_IsSet()
    {
        var refund = PaymentRefund.Create(NewAttemptId(), "reason", "issuer", "subject", Now).Value;
        var t = DateTimeOffset.UtcNow;
        refund.MarkAsFailed(t);
        refund.CompletedAt.Should().Be(t);
    }
}
