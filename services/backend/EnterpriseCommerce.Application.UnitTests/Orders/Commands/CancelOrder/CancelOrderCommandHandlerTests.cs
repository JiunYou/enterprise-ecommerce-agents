using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.CancelOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.CancelOrder;

public class CancelOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly CancelOrderCommandHandler _handler;

    public CancelOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new CancelOrderCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderExistsAndCanBeCancelled_ShouldCancelAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CancelOrderCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new CancelOrderCommand(orderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderAlreadyCancelled_ShouldReturnInvalidStatusTransition()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new CancelOrderCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
