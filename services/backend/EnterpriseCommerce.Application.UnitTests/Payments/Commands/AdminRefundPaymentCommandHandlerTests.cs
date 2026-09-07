using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Payments.Commands;

public class AdminRefundPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentAttemptRepository> _paymentAttemptRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IPaymentRefundRepository> _paymentRefundRepoMock;
    private readonly Mock<IPaymentRefundProvider> _refundProviderMock;
    private readonly IConfiguration _configuration;
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly AdminRefundPaymentCommandHandler _handler;

    public AdminRefundPaymentCommandHandlerTests()
    {
        _paymentAttemptRepoMock = new Mock<IPaymentAttemptRepository>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _paymentRefundRepoMock = new Mock<IPaymentRefundRepository>();
        _refundProviderMock = new Mock<IPaymentRefundProvider>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"]).Returns("15");
        _configuration = configMock.Object;

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _handler = new AdminRefundPaymentCommandHandler(
            _paymentAttemptRepoMock.Object,
            _orderRepoMock.Object,
            _paymentRefundRepoMock.Object,
            _refundProviderMock.Object,
            _configuration,
            timeProviderMock.Object);
    }

    private (Order order, PaymentAttempt attempt) SetupValidOrderAndAttempt(
        OrderStatus orderStatus = OrderStatus.Cancelled,
        PaymentAttemptStatus attemptStatus = PaymentAttemptStatus.RefundRequired)
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);

        if (orderStatus != OrderStatus.Pending)
        {
            var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "City", "Line1", null).Value;
            order.Submit(address, _fixedTime.AddMinutes(-30));
        }

        if (orderStatus == OrderStatus.Paid)
        {
            order.ChangeStatus(OrderStatus.Paid);
        }
        else if (orderStatus == OrderStatus.Cancelled)
        {
            order.Cancel();
        }

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(100m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            _fixedTime.AddMinutes(-30),
            "AUTH_REF_123");

        if (attemptStatus == PaymentAttemptStatus.RefundRequired)
        {
            attempt.MarkAsRefundRequired("TX_123", _fixedTime.AddMinutes(-20), "AUTH_REF_123");
        }

        _paymentAttemptRepoMock.Setup(r => r.GetByIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);
        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });

        return (order, attempt);
    }

    // ==========================================
    // 44. NEW INTENT TESTS
    // ==========================================

    [Fact]
    public async Task Handle_InvalidNewReason_Readiness0_Execution0_Intent0()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            ""); // Invalid empty reason

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PaymentErrors.InvalidRefundReason.Code, result.Error.Code);

        _refundProviderMock.Verify(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentRefundRepoMock.Verify(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessBlocked_Intent0_Execution0()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.ExecutionTemporarilyBlocked));

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.ExecutionTemporarilyBlocked, result.Value.Outcome);
        Assert.Null(result.Value.RefundStatus);

        _paymentRefundRepoMock.Verify(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessManual_Intent0_Execution0()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.ManualProviderResolutionRequired));

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.ManualProviderResolutionRequired, result.Value.Outcome);
        Assert.Null(result.Value.RefundStatus);

        _paymentRefundRepoMock.Verify(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessUnresolved_Intent0_Execution0()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Unresolved));

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.Unresolved, result.Value.Outcome);
        Assert.Null(result.Value.RefundStatus);

        _paymentRefundRepoMock.Verify(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessAlreadyRefunded_DurableIntentCreated_Execution0_FinalLocalSucceeded()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.AlreadyRefunded));

        PaymentRefund? capturedRefund = null;
        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRefund, CancellationToken>((r, _) => capturedRefund = r)
            .ReturnsAsync(true);

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);

        Assert.NotNull(capturedRefund);
        Assert.Equal(PaymentRefundStatus.Succeeded, capturedRefund!.Status);
        Assert.NotNull(capturedRefund.CompletedAt);

        _paymentRefundRepoMock.Verify(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()), Times.Once);
        _paymentRefundRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessReady_TryCreateFalse_Execution0_ReturnsConflict()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Ready));

        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Lost race

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(PaymentRefundErrors.RefundAlreadyExists.Code, result.Error.Code);

        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadinessReady_TryCreateTrue_ExecutionExactlyOnce()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Ready));

        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _refundProviderMock.Setup(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundExecutionResult(RefundExecutionOutcome.RefundCompleted));

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.RefundCompleted, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);

        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // 45. EXECUTION RESULTS TESTS
    // ==========================================

    [Theory]
    [InlineData(RefundExecutionOutcome.RefundCompleted, AdminRefundOutcome.RefundCompleted, PaymentRefundStatus.Succeeded)]
    [InlineData(RefundExecutionOutcome.AlreadyRefunded, AdminRefundOutcome.AlreadyRefunded, PaymentRefundStatus.Succeeded)]
    [InlineData(RefundExecutionOutcome.Unresolved, AdminRefundOutcome.Unresolved, PaymentRefundStatus.Unresolved)]
    [InlineData(RefundExecutionOutcome.ManualProviderResolutionRequired, AdminRefundOutcome.ManualProviderResolutionRequired, PaymentRefundStatus.Unresolved)]
    [InlineData(RefundExecutionOutcome.ExecutionTemporarilyBlocked, AdminRefundOutcome.ExecutionTemporarilyBlocked, PaymentRefundStatus.Unresolved)]
    public async Task Handle_AfterDurableIntent_MapsExecutionOutcomeCorrectly(
        RefundExecutionOutcome providerOutcome,
        AdminRefundOutcome expectedAdminOutcome,
        PaymentRefundStatus expectedFinalStatus)
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Ready));

        PaymentRefund? capturedRefund = null;
        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRefund, CancellationToken>((r, _) => capturedRefund = r)
            .ReturnsAsync(true);

        _refundProviderMock.Setup(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundExecutionResult(providerOutcome));

        var command = new AdminRefundPaymentCommand(
            attempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Valid Reason");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedAdminOutcome, result.Value.Outcome);
        Assert.Equal(expectedFinalStatus, result.Value.RefundStatus);

        Assert.NotNull(capturedRefund);
        Assert.Equal(expectedFinalStatus, capturedRefund!.Status);

        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // 46. EXISTING REFUND TESTS
    // ==========================================

    [Fact]
    public async Task Handle_ExistingSucceeded_Execute0_Reconcile0_ReturnsAlreadyRefunded()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;
        existingRefund.MarkAsSucceeded(_fixedTime);

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);

        _refundProviderMock.Verify(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingFailed_Execute0_Reconcile0_ReturnsManual()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;
        existingRefund.MarkAsFailed(_fixedTime);

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.ManualProviderResolutionRequired, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Failed, result.Value.RefundStatus);

        _refundProviderMock.Verify(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingPending_ReconcilesOnce_Execute0()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.AlreadyRefunded));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);
        Assert.Equal(PaymentRefundStatus.Succeeded, existingRefund.Status);

        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingUnresolved_ReconcileAlreadyRefunded_TransitionToSucceeded()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;
        existingRefund.MarkAsUnresolved();

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.AlreadyRefunded));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);
        Assert.Equal(PaymentRefundStatus.Succeeded, existingRefund.Status);

        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExistingPending_ReconcileUnresolved_TransitionsToUnresolved()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.Unresolved));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.Unresolved, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Unresolved, existingRefund.Status);

        _paymentRefundRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingPending_ReconcileManual_TransitionsToUnresolvedLocal_ReturnsManual()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var existingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.ManualProviderResolutionRequired));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.ManualProviderResolutionRequired, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Unresolved, existingRefund.Status);

        _paymentRefundRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // 47. CRASH-SAFETY MACHINE TESTS
    // ==========================================

    [Fact]
    public async Task ScenarioA_CrashAfterIntentBeforeProvider_ReconcilesWithoutExecute()
    {
        // Crash model: A previous request died right after TryCreateIntentAsync(Pending)
        var (_, attempt) = SetupValidOrderAndAttempt();
        var crashedPendingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;
        Assert.Equal(PaymentRefundStatus.Pending, crashedPendingRefund.Status);

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(crashedPendingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.Unresolved));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScenarioB_UnknownProviderExecution_FirstUnresolved_SecondReconcile1_ExecuteRemainsTotal1()
    {
        // 1st request: Execute returns Unresolved
        var (_, attempt) = SetupValidOrderAndAttempt();
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);

        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Ready));

        PaymentRefund? storedRefund = null;
        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRefund, CancellationToken>((r, _) => storedRefund = r)
            .ReturnsAsync(true);

        _refundProviderMock.Setup(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundExecutionResult(RefundExecutionOutcome.Unresolved));

        var cmd1 = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", "Reason 1");
        var res1 = await _handler.Handle(cmd1, CancellationToken.None);

        Assert.True(res1.IsSuccess);
        Assert.Equal(AdminRefundOutcome.Unresolved, res1.Value.Outcome);
        Assert.NotNull(storedRefund);
        Assert.Equal(PaymentRefundStatus.Unresolved, storedRefund!.Status);

        // 2nd request: Uses existing refund, calls ReconcileRefundAsync, never ExecuteRefundAsync
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.AlreadyRefunded));

        var cmd2 = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);
        var res2 = await _handler.Handle(cmd2, CancellationToken.None);

        Assert.True(res2.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, res2.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, storedRefund.Status);

        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScenarioC_ProviderSuccessLocalSaveFailureRepair_ReconcileProvesAlreadyRefunded_RepairsToSucceeded()
    {
        var (_, attempt) = SetupValidOrderAndAttempt();
        var pendingRefund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime).Value;

        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingRefund);

        _refundProviderMock.Setup(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundReconciliationResult(RefundReconciliationOutcome.AlreadyRefunded));

        var command = new AdminRefundPaymentCommand(attempt.Id.Value, "iss", "sub", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.AlreadyRefunded, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);
        Assert.Equal(PaymentRefundStatus.Succeeded, pendingRefund.Status);

        _refundProviderMock.Verify(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _refundProviderMock.Verify(p => p.ReconcileRefundAsync(It.IsAny<RefundReconciliationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Ready_UsesPersistedPaymentAttemptValuesForProviderRequests()
    {
        // 1. Arrange: 建立具有明確識別度的伺服端持久化 PaymentAttempt
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(4321m, "TWD"), 1);
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "City", "Line1", null).Value;
        order.Submit(address, _fixedTime.AddMinutes(-30));
        order.Cancel();

        var trustedAttempt = PaymentAttempt.Create(
            order.Id,
            new Money(4321m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            _fixedTime.AddMinutes(-30),
            "123456789");

        trustedAttempt.MarkAsRefundRequired("trusted-provider-transaction", _fixedTime.AddMinutes(-20), "123456789");

        _paymentAttemptRepoMock.Setup(r => r.GetByIdAsync(trustedAttempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trustedAttempt);
        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { trustedAttempt });
        _paymentRefundRepoMock.Setup(r => r.GetByPaymentAttemptIdAsync(trustedAttempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentRefund?)null);
        _paymentRefundRepoMock.Setup(r => r.TryCreateIntentAsync(It.IsAny<PaymentRefund>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        RefundExecutionRequest? capturedReadinessRequest = null;
        _refundProviderMock.Setup(p => p.CheckExecutionReadinessAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundExecutionRequest, CancellationToken>((req, _) => capturedReadinessRequest = req)
            .ReturnsAsync(new RefundReadinessResult(RefundReadinessOutcome.Ready));

        RefundExecutionRequest? capturedExecutionRequest = null;
        _refundProviderMock.Setup(p => p.ExecuteRefundAsync(It.IsAny<RefundExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundExecutionRequest, CancellationToken>((req, _) => capturedExecutionRequest = req)
            .ReturnsAsync(new RefundExecutionResult(RefundExecutionOutcome.RefundCompleted));

        // 2. Act: Command 僅提供識別碼與管理員審計資訊，完全不提供任何金流或金額資訊
        var command = new AdminRefundPaymentCommand(
            trustedAttempt.Id.Value,
            "https://auth.example.com",
            "auth0|admin-1",
            "Customer requested refund due to cancellation");

        var result = await _handler.Handle(command, CancellationToken.None);

        // 3. Assert: 結果成功
        Assert.True(result.IsSuccess);
        Assert.Equal(AdminRefundOutcome.RefundCompleted, result.Value.Outcome);
        Assert.Equal(PaymentRefundStatus.Succeeded, result.Value.RefundStatus);

        // Assert: 驗證 CheckExecutionReadinessAsync 接收到的請求完全衍生自持久化的 targetAttempt
        Assert.NotNull(capturedReadinessRequest);
        Assert.Equal(trustedAttempt.Id, capturedReadinessRequest.PaymentAttemptId);
        Assert.Equal(trustedAttempt.ProviderTransactionId, capturedReadinessRequest.ProviderTransactionId);
        Assert.Equal(trustedAttempt.ProviderAuthorizationReference, capturedReadinessRequest.ProviderAuthorizationReference);
        Assert.Equal(trustedAttempt.Amount.Amount, capturedReadinessRequest.Amount);
        Assert.Equal(trustedAttempt.Amount.Currency, capturedReadinessRequest.Currency);
        Assert.Equal(4321m, capturedReadinessRequest.Amount);
        Assert.Equal("TWD", capturedReadinessRequest.Currency);
        Assert.Equal("trusted-provider-transaction", capturedReadinessRequest.ProviderTransactionId);
        Assert.Equal("123456789", capturedReadinessRequest.ProviderAuthorizationReference);

        // Assert: 驗證 ExecuteRefundAsync 接收到的請求完全衍生自持久化的 targetAttempt
        Assert.NotNull(capturedExecutionRequest);
        Assert.Equal(trustedAttempt.Id, capturedExecutionRequest.PaymentAttemptId);
        Assert.Equal(trustedAttempt.ProviderTransactionId, capturedExecutionRequest.ProviderTransactionId);
        Assert.Equal(trustedAttempt.ProviderAuthorizationReference, capturedExecutionRequest.ProviderAuthorizationReference);
        Assert.Equal(trustedAttempt.Amount.Amount, capturedExecutionRequest.Amount);
        Assert.Equal(trustedAttempt.Amount.Currency, capturedExecutionRequest.Currency);
        Assert.Equal(4321m, capturedExecutionRequest.Amount);
        Assert.Equal("TWD", capturedExecutionRequest.Currency);
        Assert.Equal("trusted-provider-transaction", capturedExecutionRequest.ProviderTransactionId);
        Assert.Equal("123456789", capturedExecutionRequest.ProviderAuthorizationReference);

        // Assert: 反射檢查 Command 型別，確保其沒有任何金額、幣別、金流提供者、交易編號等屬性
        var commandProperties = typeof(AdminRefundPaymentCommand).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Amount", commandProperties);
        Assert.DoesNotContain("Currency", commandProperties);
        Assert.DoesNotContain("Provider", commandProperties);
        Assert.DoesNotContain("ProviderTransactionId", commandProperties);
        Assert.DoesNotContain("ProviderAuthorizationReference", commandProperties);
    }
}
