using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests.Orders;

public class OrderTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly string _currency = "USD";

    private static ShippingAddress CreateTestShippingAddress() =>
        ShippingAddress.Create(
            "Test Recipient",
            "0912345678",
            "TW",
            "100",
            "Taipei City",
            "123 Test Street",
            "Floor 4").Value;

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
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);
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
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);
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
        var result = order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

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
        var result = order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyOrder, result.Error);
    }

    [Fact]
    public void Submit_ShouldFail_WhenShippingAddressIsNull()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);

        // Act
        var result = order.Submit(null!, DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ShippingAddressRequired, result.Error);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.ShippingAddress);
    }

    [Fact]
    public void Submit_ShouldPersistShippingSnapshot_AndPreserveAcrossPaidAndCancelled()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        var shipping = CreateTestShippingAddress();

        // Act 1: Submit
        var submitResult = order.Submit(shipping, DateTimeOffset.UtcNow);
        Assert.True(submitResult.IsSuccess);
        Assert.NotNull(order.ShippingAddress);
        Assert.Equal(shipping.RecipientName, order.ShippingAddress.RecipientName);
        Assert.Equal(shipping.Phone, order.ShippingAddress.Phone);
        Assert.Equal(shipping.CountryCode, order.ShippingAddress.CountryCode);
        Assert.Equal(shipping.PostalCode, order.ShippingAddress.PostalCode);
        Assert.Equal(shipping.City, order.ShippingAddress.City);
        Assert.Equal(shipping.AddressLine1, order.ShippingAddress.AddressLine1);
        Assert.Equal(shipping.AddressLine2, order.ShippingAddress.AddressLine2);

        // Act 2: Cannot resubmit or change once submitted
        var secondSubmit = order.Submit(shipping, DateTimeOffset.UtcNow);
        Assert.True(secondSubmit.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, secondSubmit.Error);

        // Act 3: Mark as Paid preserves snapshot
        var paidResult = order.MarkAsPaid();
        Assert.True(paidResult.IsSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(shipping, order.ShippingAddress);

        // Act 4: Cancel preserves snapshot
        var cancelResult = order.Cancel();
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(shipping, order.ShippingAddress);
    }

    [Fact]
    public void MarkAsPaid_ShouldSucceed_WhenOrderIsSubmitted()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);
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
    public void Ship_ShouldSucceed_WhenOrderIsPaidAndHasShippingAddressAndValidCarrierAndTrackingNumber()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-1));
        order.MarkAsPaid();
        order.ClearDomainEvents();

        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship("  Black Cat Express  ", "  TRACK-987654  ", shippedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("Black Cat Express", order.ShippingCarrier);
        Assert.Equal("TRACK-987654", order.ShippingTrackingNumber);
        Assert.Equal(shippedAt, order.ShippedAt);

        var domainEvent = order.GetDomainEvents().SingleOrDefault(e => e is OrderStatusChangedDomainEvent) as OrderStatusChangedDomainEvent;
        Assert.NotNull(domainEvent);
        Assert.Equal(OrderStatus.Paid, domainEvent.OldStatus);
        Assert.Equal(OrderStatus.Shipped, domainEvent.NewStatus);
    }

    [Fact]
    public void Ship_ShouldFail_WhenShippingAddressIsNull()
    {
        // Arrange: construct a Paid order without shipping address
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.ChangeStatus(OrderStatus.Submitted);
        order.ChangeStatus(OrderStatus.Paid);
        Assert.Null(order.ShippingAddress);

        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship("Carrier", "TRACK-1", shippedAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ShippingAddressRequired, result.Error);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.ShippingCarrier);
        Assert.Null(order.ShippingTrackingNumber);
        Assert.Null(order.ShippedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Carrier\nName")]
    [InlineData("Carrier\rName")]
    [InlineData("Carrier\tName")]
    public void Ship_ShouldFail_WhenCarrierIsInvalid(string? invalidCarrier)
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-1));
        order.MarkAsPaid();

        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship(invalidCarrier!, "TRACK-1", shippedAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingCarrier, result.Error);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.ShippingCarrier);
        Assert.Null(order.ShippingTrackingNumber);
        Assert.Null(order.ShippedAt);
    }

    [Fact]
    public void Ship_ShouldFail_WhenCarrierExceeds100Characters()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-1));
        order.MarkAsPaid();

        var carrierTooLong = new string('A', 101);
        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship(carrierTooLong, "TRACK-1", shippedAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingCarrier, result.Error);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.ShippingCarrier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("TRACK\n123")]
    [InlineData("TRACK\r123")]
    [InlineData("TRACK\t123")]
    public void Ship_ShouldFail_WhenTrackingNumberIsInvalid(string? invalidTracking)
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-1));
        order.MarkAsPaid();

        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship("Carrier", invalidTracking!, shippedAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingTrackingNumber, result.Error);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.ShippingCarrier);
        Assert.Null(order.ShippingTrackingNumber);
        Assert.Null(order.ShippedAt);
    }

    [Fact]
    public void Ship_ShouldFail_WhenTrackingNumberExceeds100Characters()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-1));
        order.MarkAsPaid();

        var trackingTooLong = new string('T', 101);
        var shippedAt = DateTimeOffset.UtcNow;

        // Act
        var result = order.Ship("Carrier", trackingTooLong, shippedAt);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingTrackingNumber, result.Error);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.ShippingTrackingNumber);
    }

    [Fact]
    public void Ship_ShouldFail_WhenOrderIsPending()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);

        // Act
        var result = order.Ship("Carrier", "TRACK-1", DateTimeOffset.UtcNow);

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
        var result = order.Ship("Carrier", "TRACK-1", DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, result.Error);
    }

    [Fact]
    public void Ship_ShouldFail_WhenOrderIsAlreadyShipped()
    {
        // Arrange
        var order = Order.Create(_customerId, _currency);
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, _currency), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow.AddMinutes(-2));
        order.MarkAsPaid();

        var firstShipTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        var firstResult = order.Ship("Initial Carrier", "INIT-123", firstShipTime);
        Assert.True(firstResult.IsSuccess);

        // Act
        var secondResult = order.Ship("Second Carrier", "SECOND-456", DateTimeOffset.UtcNow);

        // Assert
        Assert.True(secondResult.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition, secondResult.Error);
        Assert.Equal("Initial Carrier", order.ShippingCarrier);
        Assert.Equal("INIT-123", order.ShippingTrackingNumber);
        Assert.Equal(firstShipTime, order.ShippedAt);
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
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

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
