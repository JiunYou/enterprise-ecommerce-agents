using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence.Repositories;

public class InventoryRepositoryTests
{
    private readonly EnterpriseCommerceDbContext _dbContext;
    private readonly InventoryRepository _repository;

    public InventoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(options);
        _repository = new InventoryRepository(_dbContext);
    }

    [Fact]
    public async Task GetByProductIdAsync_ShouldReturnItem_WhenItExists()
    {
        // Arrange
        var productReference = new ProductReference(Guid.NewGuid());
        var inventoryItem = InventoryItem.Create(productReference);
        inventoryItem.IncreaseStock(new StockQuantity(100));

        _dbContext.InventoryItems.Add(inventoryItem);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByProductIdAsync(productReference);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(inventoryItem.Id);
        result.AvailableQuantity.Value.Should().Be(100);
    }

    [Fact]
    public async Task GetByProductIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var productReference = new ProductReference(Guid.NewGuid());

        // Act
        var result = await _repository.GetByProductIdAsync(productReference);

        // Assert
        result.Should().BeNull();
    }
}
