using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests.Orders;

public class OrderTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly string _currency = "USD";

    [Fact]
    public void Create_ShouldCreateOrderWithPendingStatusAndRaiseEvent()
    {
        // Act
        var order = Order.Create(_customerId, _currency);

        // Assert
        Assert.NotEqual(Guid.Empty, order.Id.Value);
        Assert.Equal(_customerId, order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(order.Items);
        Assert.Equal(Money.Zero(_currency), order.TotalAmount);
        
        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderCreatedDomainEvent) as OrderCreatedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(order.Id, domainEvent.OrderId);
        Assert.Equal(_customerId, domainEvent.CustomerId);
    }

    [Fact]
    public void AddItem_ShouldAddOrderItemAndIncreaseTotalAmount()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(100, _currency);

        // Act
        var result = order.AddItem(productId, price, 2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(order.Items);
        Assert.Equal(new Money(200, _currency), order.TotalAmount);
    }

    [Fact]
    public void AddItem_ShouldFail_WhenQuantityIsZeroOrNegative()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(100, _currency);

        // Act
        var result = order.AddItem(productId, price, 0);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
    }

    [Fact]
    public void AddItem_ShouldFail_WhenOrderIsNotPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(10, _currency), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        // Act
        var result = order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void ChangeStatus_ShouldFail_WhenOrderIsEmptyAndStatusIsNotCancelled()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);

        // Act
        var result = order.ChangeStatus(OrderStatus.Paid);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder, result.Error);
    }

    [Fact]
    public void ChangeStatus_ShouldSucceed_AndRaiseEvent_WhenTransitionIsValid()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(10, _currency), 1);
        order.ClearDomainEvents();

        // Act
        var result = order.ChangeStatus(OrderStatus.Submitted);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Submitted, order.Status);

        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderStatusChangedDomainEvent) as OrderStatusChangedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(OrderStatus.Pending, domainEvent.OldStatus);
        Assert.Equal(OrderStatus.Submitted, domainEvent.NewStatus);
    }

    [Fact]
    public void ChangeStatus_ShouldFail_WhenTransitionIsInvalid_PendingToShipped()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(10, _currency), 1);

        // Act
        var result = order.ChangeStatus(OrderStatus.Shipped);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void Cancel_ShouldSucceed_WhenOrderIsPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);

        // Act
        var result = order.Cancel();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_ShouldFail_WhenOrderIsAlreadyCancelled()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.Cancel();

        // Act
        var result = order.Cancel();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void RemoveItem_ShouldSucceed_WhenOrderIsPendingAndItemExists()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50, _currency), 1);

        // Act
        var result = order.RemoveItem(productId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(order.Items);
        Assert.Equal(Money.Zero(_currency), order.TotalAmount);
    }

    [Fact]
    public void RemoveItem_ShouldFail_WhenOrderIsNotPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50, _currency), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        // Act
        var result = order.RemoveItem(productId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void RemoveItem_ShouldFail_WhenItemNotFound()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());

        // Act
        var result = order.RemoveItem(productId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ItemNotFound, result.Error);
    }

    [Fact]
    public void Submit_ShouldSucceed_WhenOrderHasItemsAndIsPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.ClearDomainEvents();

        // Act
        var result = order.Submit(DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Submitted, order.Status);

        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderStatusChangedDomainEvent) as OrderStatusChangedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(OrderStatus.Pending, domainEvent.OldStatus);
        Assert.Equal(OrderStatus.Submitted, domainEvent.NewStatus);
    }

    [Fact]
    public void Submit_ShouldFail_WhenOrderIsEmpty()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);

        // Act
        var result = order.Submit(DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder, result.Error);
    }

    [Fact]
    public void MarkAsPaid_ShouldSucceed_WhenOrderIsSubmitted()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.ClearDomainEvents();

        // Act
        var result = order.MarkAsPaid();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);

        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderStatusChangedDomainEvent) as OrderStatusChangedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(OrderStatus.Submitted, domainEvent.OldStatus);
        Assert.Equal(OrderStatus.Paid, domainEvent.NewStatus);
    }

    [Fact]
    public void MarkAsPaid_ShouldFail_WhenOrderIsPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);

        // Act
        var result = order.MarkAsPaid();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void Ship_ShouldSucceed_WhenOrderIsPaid()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.MarkAsPaid();
        order.ClearDomainEvents();

        // Act
        var result = order.Ship();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.Status);

        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderStatusChangedDomainEvent) as OrderStatusChangedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(OrderStatus.Paid, domainEvent.OldStatus);
        Assert.Equal(OrderStatus.Shipped, domainEvent.NewStatus);
    }

    [Fact]
    public void Ship_ShouldFail_WhenOrderIsPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);

        // Act
        var result = order.Ship();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void Ship_ShouldFail_WhenOrderIsCancelled()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Cancel();

        // Act
        var result = order.Ship();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void AddItem_ShouldIncreaseQuantity_WhenAddingSameProductAgain()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(50, _currency);

        // Act
        var result1 = order.AddItem(productId, price, 2);
        var result2 = order.AddItem(productId, price, 3);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Single(order.Items);
        var item = order.Items.First();
        Assert.Equal(5, item.Quantity);
        Assert.Equal(new Money(50, _currency), item.UnitPrice);
        Assert.Equal(new Money(250, _currency), order.TotalAmount);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldSucceed_WhenValidQuantity()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(40, _currency);
        order.AddItem(productId, price, 2);

        // Act
        var result = order.UpdateItemQuantity(productId, 5);

        // Assert
        Assert.True(result.IsSuccess);
        var item = order.Items.First();
        Assert.Equal(5, item.Quantity);
        Assert.Equal(new Money(200, _currency), order.TotalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateItemQuantity_ShouldFail_WhenQuantityIsZeroOrNegative(int invalidQuantity)
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(40, _currency);
        order.AddItem(productId, price, 2);

        // Act
        var result = order.UpdateItemQuantity(productId, invalidQuantity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidQuantity, result.Error);
        Assert.Equal(2, order.Items.First().Quantity);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldFail_WhenItemNotFound()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var existingProductId = new ProductId(Guid.NewGuid());
        var nonExistentProductId = new ProductId(Guid.NewGuid());
        order.AddItem(existingProductId, new Money(40, _currency), 2);

        // Act
        var result = order.UpdateItemQuantity(nonExistentProductId, 3);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ItemNotFound, result.Error);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldFail_WhenOrderIsNotPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(40, _currency), 2);
        order.Submit(DateTimeOffset.UtcNow);

        // Act
        var result = order.UpdateItemQuantity(productId, 5);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void AddItem_ShouldPreserveExistingUnitPrice_WhenAddingSameProductWithDifferentPrice()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var originalPrice = new Money(50, _currency);
        var differentPrice = new Money(80, _currency);

        // Act
        order.AddItem(productId, originalPrice, 2);
        order.AddItem(productId, differentPrice, 3);

        // Assert
        Assert.Single(order.Items);
        var item = order.Items.First();
        Assert.Equal(5, item.Quantity);
        Assert.Equal(originalPrice, item.UnitPrice);
        Assert.Equal(new Money(250, _currency), order.TotalAmount);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldPreserveExistingUnitPrice_WhenQuantityChanges()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        var productId = new ProductId(Guid.NewGuid());
        var price = new Money(50, _currency);
        order.AddItem(productId, price, 2);

        // Act
        order.UpdateItemQuantity(productId, 10);

        // Assert
        Assert.Single(order.Items);
        var item = order.Items.First();
        Assert.Equal(10, item.Quantity);
        Assert.Equal(price, item.UnitPrice);
        Assert.Equal(new Money(500, _currency), order.TotalAmount);
    }
}
