using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Inventory.Commands.IncreaseInventoryStock;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Inventory.Commands;

public class IncreaseInventoryStockCommandHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly IncreaseInventoryStockCommandHandler _handler;

    public IncreaseInventoryStockCommandHandlerTests()
    {
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new IncreaseInventoryStockCommandHandler(
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidPositiveQuantity_IncreasesAvailableAndLeavesReservedUnchanged()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(new StockQuantity(10));

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        var command = new IncreaseInventoryStockCommand(productId, 5);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        inventoryItem.AvailableQuantity.Value.Should().Be(15);
        inventoryItem.ReservedQuantity.Value.Should().Be(0);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingInventory_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var command = new IncreaseInventoryStockCommand(productId, 10);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.NotFound);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Handle_ZeroOrNegativeQuantity_ReturnsNegativeQuantityFailure(int invalidQuantity)
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        var command = new IncreaseInventoryStockCommand(productId, invalidQuantity);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.NegativeQuantity);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrencyException_ReturnsConcurrencyConflictAndDoesNotRetry()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        _unitOfWorkMock.Setup(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new IncreaseInventoryStockCommand(productId, 10);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ConcurrencyConflict);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once, "Should not retry on concurrency conflict");
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }
}
