using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Inventory.Queries.GetInventoryByProductId;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Inventory.Queries;

public class GetInventoryByProductIdQueryHandlerTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock;
    private readonly GetInventoryByProductIdQueryHandler _handler;

    public GetInventoryByProductIdQueryHandlerTests()
    {
        _inventoryRepositoryMock = new Mock<IInventoryRepository>();
        _handler = new GetInventoryByProductIdQueryHandler(_inventoryRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingInventory_ReturnsExpectedStockValues()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(new StockQuantity(25));

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventoryItem);

        var query = new GetInventoryByProductIdQuery(productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.AvailableQuantity.Should().Be(25);
        result.Value.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MissingInventory_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productReference = new ProductReference(productId);

        _inventoryRepositoryMock.Setup(repo => repo.GetByProductIdAsync(productReference, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var query = new GetInventoryByProductIdQuery(productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.NotFound);
    }
}
