using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.MarkOrderAsPaid;

public class MarkOrderAsPaidCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly MarkOrderAsPaidCommandHandler _handler;

    public MarkOrderAsPaidCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new MarkOrderAsPaidCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderHasItemsAndIsPending_ShouldMarkAsPaidAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new MarkOrderAsPaidCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsEmpty_ShouldReturnEmptyOrder()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new MarkOrderAsPaidCommand(order.Id.Value);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new MarkOrderAsPaidCommand(orderId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
