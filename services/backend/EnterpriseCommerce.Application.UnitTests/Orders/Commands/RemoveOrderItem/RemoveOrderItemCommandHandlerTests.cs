using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.RemoveOrderItem;

public class RemoveOrderItemCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly RemoveOrderItemCommandHandler _handler;

    public RemoveOrderItemCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new RemoveOrderItemCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderAndItemExist_ShouldRemoveItemAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(100, "TWD"), 2);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RemoveOrderItemCommand(order.Id.Value, productId.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(order.Items);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new RemoveOrderItemCommand(orderId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenItemNotFound_ShouldReturnItemNotFound()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new RemoveOrderItemCommand(order.Id.Value, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ItemNotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
