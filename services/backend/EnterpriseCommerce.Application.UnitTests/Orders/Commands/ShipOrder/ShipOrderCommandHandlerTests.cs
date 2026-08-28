using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.ShipOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.ShipOrder;

public class ShipOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly ShipOrderCommandHandler _handler;

    public ShipOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new ShipOrderCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaid_ShouldShipAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new ShipOrderCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPending_ShouldReturnInvalidStatusTransition()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new ShipOrderCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new ShipOrderCommand(orderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
