using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.Events;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;

namespace EnterpriseCommerce.Domain.UnitTests.Inventory;

public class InventoryItemTests
{
    private readonly ProductReference _productReference = new(Guid.NewGuid());

    [Fact]
    public void Create_ShouldInitializeInventoryAndRaiseEvent()
    {
        // Act
        var item = InventoryItem.Create(_productReference);

        // Assert
        Assert.NotEqual(Guid.Empty, item.Id.Value);
        Assert.Equal(_productReference, item.ProductReference);
        Assert.Equal(0, item.AvailableQuantity.Value);
        Assert.Equal(0, item.ReservedQuantity.Value);
        
        var domainEvent = item.GetDomainEvents().SingleOrDefault(e => e is InventoryItemCreatedDomainEvent) as InventoryItemCreatedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(item.Id, domainEvent.InventoryId);
    }

    [Fact]
    public void IncreaseStock_ShouldAddQuantityAndRaiseEvent()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.ClearDomainEvents();

        // Act
        var result = item.IncreaseStock(10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, item.AvailableQuantity.Value);

        var domainEvent = item.GetDomainEvents().SingleOrDefault(e => e is StockIncreasedDomainEvent) as StockIncreasedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(10, domainEvent.AddedQuantity.Value);
        Assert.Equal(10, domainEvent.NewAvailableQuantity.Value);
    }

    [Fact]
    public void DecreaseStock_ShouldReduceQuantityAndRaiseEvent()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(10);
        item.ClearDomainEvents();

        // Act
        var result = item.DecreaseStock(4);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(6, item.AvailableQuantity.Value);

        var domainEvent = item.GetDomainEvents().SingleOrDefault(e => e is StockDecreasedDomainEvent) as StockDecreasedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(4, domainEvent.RemovedQuantity.Value);
        Assert.Equal(6, domainEvent.NewAvailableQuantity.Value);
    }

    [Fact]
    public void DecreaseStock_ShouldFail_WhenInsufficientStock()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(5);

        // Act
        var result = item.DecreaseStock(10);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(5, item.AvailableQuantity.Value);
    }

    [Fact]
    public void ReserveStock_ShouldMoveAvailableToReservedAndRaiseEvent()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(10);
        item.ClearDomainEvents();

        // Act
        var orderId = new OrderReference(Guid.NewGuid());
        var result = item.ReserveStock(orderId, 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(7, item.AvailableQuantity.Value);
        Assert.Equal(3, item.ReservedQuantity.Value);

        var domainEvent = item.GetDomainEvents().SingleOrDefault(e => e is StockReservedDomainEvent) as StockReservedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(3, domainEvent.ReservedQuantity.Value);
        Assert.Equal(7, domainEvent.NewAvailableQuantity.Value);
        Assert.Equal(3, domainEvent.TotalReservedQuantity.Value);
    }

    [Fact]
    public void ReserveStock_ShouldFail_WhenInsufficientStock()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(5);

        // Act
        var orderId = new OrderReference(Guid.NewGuid());
        var result = item.ReserveStock(orderId, 10);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(5, item.AvailableQuantity.Value);
        Assert.Equal(0, item.ReservedQuantity.Value);
    }

    [Fact]
    public void ReleaseReservation_ShouldMoveReservedToAvailableAndRaiseEvent()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(10);
        var orderId = new OrderReference(Guid.NewGuid());
        item.ReserveStock(orderId, 5);
        item.ClearDomainEvents();

        // Act
        var result = item.ReleaseReservation(orderId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(10, item.AvailableQuantity.Value);
        Assert.Equal(0, item.ReservedQuantity.Value);

        var domainEvent = item.GetDomainEvents().SingleOrDefault(e => e is StockReservationReleasedDomainEvent) as StockReservationReleasedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(5, domainEvent.ReleasedQuantity.Value);
        Assert.Equal(10, domainEvent.NewAvailableQuantity.Value);
        Assert.Equal(0, domainEvent.TotalReservedQuantity.Value);
    }

    [Fact]
    public void ReleaseReservation_ShouldBeIdempotent_WhenReservationDoesNotExist()
    {
        // Arrange
        var item = InventoryItem.Create(_productReference);
        item.IncreaseStock(10);
        var orderId = new OrderReference(Guid.NewGuid());
        item.ReserveStock(orderId, 5);

        // Act
        // Releasing a non-existent reservation should succeed without changing quantities
        var otherOrderId = new OrderReference(Guid.NewGuid());
        var result = item.ReleaseReservation(otherOrderId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, item.AvailableQuantity.Value);
        Assert.Equal(5, item.ReservedQuantity.Value);
    }
}
