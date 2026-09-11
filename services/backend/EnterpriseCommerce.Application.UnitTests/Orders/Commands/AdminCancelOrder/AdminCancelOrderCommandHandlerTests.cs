using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.AdminCancelOrder;

public class AdminCancelOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IAdminOrderCancellationStore> _cancellationStoreMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPaymentAttemptRepository> _paymentAttemptRepoMock;
    private readonly DateTimeOffset _fixedUtcNow;
    private readonly TimeProvider _timeProvider;
    private readonly AdminCancelOrderCommandHandler _handler;

    public AdminCancelOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _cancellationStoreMock = new Mock<IAdminOrderCancellationStore>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _paymentAttemptRepoMock = new Mock<IPaymentAttemptRepository>();
        _fixedUtcNow = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        _timeProvider = new FixedTimeProvider(_fixedUtcNow);

        _handler = new AdminCancelOrderCommandHandler(
            _orderRepositoryMock.Object,
            _cancellationStoreMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _paymentAttemptRepoMock.Object);
    }

    private PaymentAttempt CreateAttempt(OrderId orderId, PaymentAttemptStatus status, string txId = "tx-123")
    {
        var attempt = PaymentAttempt.Create(
            orderId,
            new Money(100m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-10));

        switch (status)
        {
            case PaymentAttemptStatus.Succeeded:
                attempt.MarkAsSucceeded(txId, DateTimeOffset.UtcNow.AddMinutes(-8), "AUTH_REF");
                break;
            case PaymentAttemptStatus.Failed:
                attempt.MarkAsFailed(txId, DateTimeOffset.UtcNow.AddMinutes(-8));
                break;
            case PaymentAttemptStatus.RefundRequired:
                attempt.MarkAsRefundRequired(txId, DateTimeOffset.UtcNow.AddMinutes(-8), "AUTH_REF");
                break;
            case PaymentAttemptStatus.Pending:
                break;
        }

        return attempt;
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new AdminCancelOrderCommand(orderId, "https://auth.example.com/", "auth0|admin-1", "Fraud suspicion");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(OrderErrors.NotFound.Code);
        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPending_ShouldCancelAndAddAuditAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "  Customer requested phone cancel  ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);

        _cancellationStoreMock.Verify(s => s.Add(It.Is<AdminOrderCancellationAudit>(a =>
            a.OrderId == order.Id.Value &&
            a.ActorIssuer == "https://auth.example.com/" &&
            a.ActorSubject == "auth0|admin-1" &&
            a.CancelledAt == _fixedUtcNow &&
            a.Reason == "Customer requested phone cancel")), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsSubmitted_ShouldCancelAndAddAuditAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-2",
            "Order verification failed");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);

        _cancellationStoreMock.Verify(s => s.Add(It.Is<AdminOrderCancellationAudit>(a =>
            a.OrderId == order.Id.Value &&
            a.ActorIssuer == "https://auth.example.com/" &&
            a.ActorSubject == "auth0|admin-2" &&
            a.CancelledAt == _fixedUtcNow &&
            a.Reason == "Order verification failed")), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_WithExactlyOneSucceededAttempt_ShouldCancelOrder_MarkAttemptRefundRequired_AndAddAudit()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var succeededAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-paid-1");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([succeededAttempt]);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Customer requested refund");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        succeededAttempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired);

        _cancellationStoreMock.Verify(s => s.Add(It.Is<AdminOrderCancellationAudit>(a =>
            a.OrderId == order.Id.Value &&
            a.ActorIssuer == "https://auth.example.com/" &&
            a.ActorSubject == "auth0|admin-1" &&
            a.CancelledAt == _fixedUtcNow &&
            a.Reason == "Customer requested refund")), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_WithMultipleSucceededAttempts_ShouldFailClosed_AndNotCancel()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var succeededAttempt1 = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-1");
        var succeededAttempt2 = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-2");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([succeededAttempt1, succeededAttempt2]);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Customer requested refund");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.RefundOrderPaymentInvariantViolation");
        order.Status.Should().Be(OrderStatus.Paid);
        succeededAttempt1.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        succeededAttempt2.Status.Should().Be(PaymentAttemptStatus.Succeeded);

        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_WithNoSucceededAttempt_ShouldFailClosed_AndNotCancel()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var failedAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Failed, "tx-failed");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([failedAttempt]);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Customer requested refund");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.RefundOrderPaymentInvariantViolation");
        order.Status.Should().Be(OrderStatus.Paid);

        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_WithOneSucceededAndOneExistingRefundRequired_ShouldCancelAndOnlyTransitionSucceeded()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var succeededAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-succeeded");
        var existingRefundReq = CreateAttempt(order.Id, PaymentAttemptStatus.RefundRequired, "tx-existing-refund");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([succeededAttempt, existingRefundReq]);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Customer requested refund");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        succeededAttempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired);
        existingRefundReq.Status.Should().Be(PaymentAttemptStatus.RefundRequired);

        _cancellationStoreMock.Verify(s => s.Add(It.Is<AdminOrderCancellationAudit>(a =>
            a.OrderId == order.Id.Value &&
            a.ActorIssuer == "https://auth.example.com/" &&
            a.ActorSubject == "auth0|admin-1" &&
            a.CancelledAt == _fixedUtcNow &&
            a.Reason == "Customer requested refund")), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_WithOneSucceededAndPendingAndFailed_ShouldCancelAndLeavePendingAndFailedUntouched()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var succeededAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-succeeded");
        var pendingAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Pending, "tx-pending");
        var failedAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Failed, "tx-failed");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([succeededAttempt, pendingAttempt, failedAttempt]);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Customer requested refund");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        succeededAttempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired);
        pendingAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);
        failedAttempt.Status.Should().Be(PaymentAttemptStatus.Failed);

        _cancellationStoreMock.Verify(s => s.Add(It.Is<AdminOrderCancellationAudit>(a =>
            a.OrderId == order.Id.Value &&
            a.ActorIssuer == "https://auth.example.com/" &&
            a.ActorSubject == "auth0|admin-1" &&
            a.CancelledAt == _fixedUtcNow &&
            a.Reason == "Customer requested refund")), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_ConcurrencyConflict_ShouldReturnConflictError()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        var succeededAttempt = CreateAttempt(order.Id, PaymentAttemptStatus.Succeeded, "tx-succeeded");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([succeededAttempt]);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Concurrency test");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.ConcurrencyConflict");
    }

    [Fact]
    public async Task Handle_WhenOrderIsShipped_ShouldRejectAndNotCancelNorAddAudit()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();
        order.Ship();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Shipped order cancel attempt");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(OrderErrors.InvalidStatusTransition.Code);
        order.Status.Should().Be(OrderStatus.Shipped);
        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderIsAlreadyCancelled_ShouldRejectAndNotAddAudit()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Double cancel attempt");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(OrderErrors.InvalidStatusTransition.Code);
        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDbUpdateConcurrencyExceptionOccurs_ShouldReturnConflictResultAndNotRetry()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Valid reason");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once, "Should not retry on poisoned state");
    }

    private static ShippingAddress CreateTestAddress()
    {
        return ShippingAddress.Create(
            "Alice",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Test Rd").Value;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
