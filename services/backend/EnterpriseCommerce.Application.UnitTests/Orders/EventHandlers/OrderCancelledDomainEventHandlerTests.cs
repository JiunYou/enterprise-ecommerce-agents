using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;
using EnterpriseCommerce.Application.Orders.EventHandlers;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.EventHandlers;

public class OrderCancelledDomainEventHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<ISender> _senderMock;
    private readonly Mock<ILogger<OrderCancelledDomainEventHandler>> _loggerMock;
    private readonly OrderCancelledDomainEventHandler _handler;

    public OrderCancelledDomainEventHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _senderMock = new Mock<ISender>();
        _loggerMock = new Mock<ILogger<OrderCancelledDomainEventHandler>>();
        _handler = new OrderCancelledDomainEventHandler(_orderRepositoryMock.Object, _senderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenStatusIsNotCancelled()
    {
        // Arrange
        var notification = new OrderStatusChangedDomainEvent(
            new OrderId(Guid.NewGuid()), 
            OrderStatus.Pending, 
            OrderStatus.Shipped);

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert
        _orderRepositoryMock.Verify(repo => repo.GetByIdAsync(It.IsAny<OrderId>(), It.IsAny<CancellationToken>()), Times.Never);
        _senderMock.Verify(sender => sender.Send(It.IsAny<IRequest<Result>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldDispatchReleaseCommand_WhenStatusIsCancelled()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId1 = new ProductId(Guid.NewGuid());
        var productId2 = new ProductId(Guid.NewGuid());
        
        order.AddItem(productId1, new Money(100m, "USD"), 2);
        order.AddItem(productId2, new Money(50m, "USD"), 1);
        
        order.Cancel(); // Status becomes Cancelled

        var notification = new OrderStatusChangedDomainEvent(
            order.Id, 
            OrderStatus.Pending, 
            OrderStatus.Cancelled);

        _orderRepositoryMock.Setup(repo => repo.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
            
        _senderMock.Setup(sender => sender.Send(It.IsAny<ReleaseInventoryReservationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert
        _orderRepositoryMock.Verify(repo => repo.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(sender => sender.Send(It.Is<ReleaseInventoryReservationCommand>(c => c.OrderId == order.Id.Value && c.ProductId == productId1.Value), It.IsAny<CancellationToken>()), Times.Once);
        _senderMock.Verify(sender => sender.Send(It.Is<ReleaseInventoryReservationCommand>(c => c.OrderId == order.Id.Value && c.ProductId == productId2.Value), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenReleaseCommandFails()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        
        order.AddItem(productId, new Money(100m, "USD"), 2);
        order.Cancel();

        var notification = new OrderStatusChangedDomainEvent(
            order.Id, 
            OrderStatus.Pending, 
            OrderStatus.Cancelled);

        _orderRepositoryMock.Setup(repo => repo.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
            
        _senderMock.Setup(sender => sender.Send(It.IsAny<ReleaseInventoryReservationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("Test.Error", "Release failed")));

        // Act & Assert
        var act = async () => await _handler.HandleAsync(notification, CancellationToken.None);
        
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to release inventory: Release failed");
    }
}
