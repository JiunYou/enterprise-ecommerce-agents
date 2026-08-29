using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.SubmitOrder;

public class SubmitOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly SubmitOrderCommandHandler _handler;

    public SubmitOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new SubmitOrderCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCustomerMismatch_ShouldReturnNotFound()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var differentCustomerId = Guid.NewGuid();
        var command = new SubmitOrderCommand(order.Id.Value, differentCustomerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _inventoryRepositoryMock.Verify(r => r.GetByProductIdForUpdateAsync(It.IsAny<EnterpriseCommerce.Domain.Inventory.ValueObjects.ProductReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
