using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
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
        _inventoryRepositoryMock.Verify(r => r.GetByProductIdForUpdateAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidOrder_ShouldReserveInventoryAndCommitTransaction()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = Guid.NewGuid();
        order.AddItem(new ProductId(productId), new Money(100, "TWD"), 2);

        var inventoryItem = InventoryItem.Create(new ProductReference(productId));
        inventoryItem.IncreaseStock(new StockQuantity(50));

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdForUpdateAsync(
                It.Is<ProductReference>(p => p.Value == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        var command = new SubmitOrderCommand(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Equal(48, inventoryItem.AvailableQuantity.Value);
        Assert.Equal(2, inventoryItem.ReservedQuantity.Value);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsAlreadySubmitted_ShouldReturnInvalidStatusTransition()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = Guid.NewGuid();
        order.AddItem(new ProductId(productId), new Money(100, "TWD"), 2);
        order.Submit(DateTimeOffset.UtcNow);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new SubmitOrderCommand(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error.Code);
        Assert.Equal(OrderStatus.Submitted, order.Status);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderIsEmpty_ShouldReturnEmptyOrder()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new SubmitOrderCommand(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder.Code, result.Error.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenInsufficientStock_ShouldRollbackAndLeaveOrderPending()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = Guid.NewGuid();
        order.AddItem(new ProductId(productId), new Money(100, "TWD"), 5);

        var inventoryItem = InventoryItem.Create(new ProductReference(productId));
        inventoryItem.IncreaseStock(new StockQuantity(2)); // Available only 2, requires 5

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdForUpdateAsync(
                It.Is<ProductReference>(p => p.Value == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        var command = new SubmitOrderCommand(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock.Code, result.Error.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, inventoryItem.AvailableQuantity.Value);
        Assert.Equal(0, inventoryItem.ReservedQuantity.Value);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMultipleItemsAndSecondFails_ShouldRollbackAllAndNotCommit()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");

        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        // Ensure deterministic ordering
        var sortedProductIds = new[] { product1Id, product2Id }.OrderBy(x => x).ToArray();
        var firstProductId = sortedProductIds[0];
        var secondProductId = sortedProductIds[1];

        order.AddItem(new ProductId(firstProductId), new Money(100, "TWD"), 2);
        order.AddItem(new ProductId(secondProductId), new Money(200, "TWD"), 10);

        var inventory1 = InventoryItem.Create(new ProductReference(firstProductId));
        inventory1.IncreaseStock(new StockQuantity(20)); // Has enough

        var inventory2 = InventoryItem.Create(new ProductReference(secondProductId));
        inventory2.IncreaseStock(new StockQuantity(1)); // Insufficient: has 1, requires 10

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdForUpdateAsync(
                It.Is<ProductReference>(p => p.Value == firstProductId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory1);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdForUpdateAsync(
                It.Is<ProductReference>(p => p.Value == secondProductId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventory2);

        var command = new SubmitOrderCommand(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock.Code, result.Error.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
