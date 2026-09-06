using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
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
    private readonly DateTimeOffset _fixedUtcNow;
    private readonly TimeProvider _timeProvider;
    private readonly AdminCancelOrderCommandHandler _handler;

    public AdminCancelOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _cancellationStoreMock = new Mock<IAdminOrderCancellationStore>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _fixedUtcNow = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        _timeProvider = new FixedTimeProvider(_fixedUtcNow);

        _handler = new AdminCancelOrderCommandHandler(
            _orderRepositoryMock.Object,
            _cancellationStoreMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider);
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
    public async Task Handle_WhenOrderIsPaid_ShouldRejectAndNotCancelNorAddAudit()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new AdminCancelOrderCommand(
            order.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Paid order cancel attempt");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.CannotCancelPaidOrder");
        result.Error.Message.Should().Contain("Paid orders cannot be cancelled by this operation");
        order.Status.Should().Be(OrderStatus.Paid);
        _cancellationStoreMock.Verify(s => s.Add(It.IsAny<AdminOrderCancellationAudit>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
