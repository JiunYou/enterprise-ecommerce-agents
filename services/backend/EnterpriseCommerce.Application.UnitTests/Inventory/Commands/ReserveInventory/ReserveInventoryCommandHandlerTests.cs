using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Inventory.Commands.ReserveInventory;

public class ReserveInventoryCommandHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly ReserveInventoryCommandHandler _handler;

    public ReserveInventoryCommandHandlerTests()
    {
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new ReserveInventoryCommandHandler(_inventoryRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_ReserveStock_And_SaveChanges_When_Valid()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new ReserveInventoryCommand(productId, Guid.NewGuid(), 5);
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(new StockQuantity(10));

        _inventoryRepositoryMock
            .Setup(x => x.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, inventoryItem.ReservedQuantity.Value);
        Assert.Equal(5, inventoryItem.AvailableQuantity.Value);

        _inventoryRepositoryMock.Verify(x => x.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == productId), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_InventoryItemNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new ReserveInventoryCommand(productId, Guid.NewGuid(), 5);

        _inventoryRepositoryMock
            .Setup(x => x.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Inventory.NotFound", result.Error.Code);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_DomainReserveStockFails()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var command = new ReserveInventoryCommand(productId, Guid.NewGuid(), 15); // requesting more than available
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(new StockQuantity(10));

        _inventoryRepositoryMock
            .Setup(x => x.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Inventory.InsufficientStock", result.Error.Code);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
