using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using System.Reflection;

namespace EnterpriseCommerce.Application.UnitTests.Inventory.Commands;

public class ReleaseInventoryReservationCommandHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly ReleaseInventoryReservationCommandHandler _handler;

    public ReleaseInventoryReservationCommandHandlerTests()
    {
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new ReleaseInventoryReservationCommandHandler(_inventoryRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenInventoryItemDoesNotExist()
    {
        // Arrange
        var command = new ReleaseInventoryReservationCommand(Guid.NewGuid(), Guid.NewGuid());

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessAndSaveChanges_WhenReservationIsReleased()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new ReleaseInventoryReservationCommand(productId, orderId);

        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(10);
        inventoryItem.ReserveStock(new OrderReference(orderId), 5);

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        inventoryItem.ReservedQuantity.Value.Should().Be(0);
        inventoryItem.AvailableQuantity.Value.Should().Be(10);
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenReservationWasAlreadyReleased()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new ReleaseInventoryReservationCommand(productId, orderId);

        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(10);
        // Do not reserve anything to simulate already released or never reserved

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Releasing a non-existent reservation is idempotent and succeeds
        result.IsSuccess.Should().BeTrue();
        _unitOfWorkMock.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
