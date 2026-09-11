using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetOrderByIdQueryHandler _handler;

    public GetOrderByIdQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetOrderByIdQueryHandler(_orderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderExists_ShouldReturnSuccessWithOrderResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = new ProductId(Guid.NewGuid());
        var unitPrice = new Money(100m, "TWD");
        order.AddItem(productId, unitPrice, 2);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, order.CustomerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Id.Value, result.Value.Id);
        Assert.Equal(customerId, result.Value.CustomerId);
        Assert.Equal("TWD", result.Value.Currency);
        Assert.Equal(200m, result.Value.TotalAmount);
        Assert.Single(result.Value.Items);

        var item = result.Value.Items.First();
        Assert.Equal(productId.Value, item.ProductId);
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(200m, item.TotalPrice);
    }

    [Fact]
    public async Task Handle_WhenOrderDoesNotExist_ShouldReturnFailureWithNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var query = new GetOrderByIdQuery(orderId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
    }
    [Fact]
    public async Task Handle_WhenCustomerMismatch_ShouldReturnNotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var differentCustomerId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(order.Id.Value, differentCustomerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenOrderIsShippedWithCompleteTracking_ReturnsShipmentTrackingResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        var shipping = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "St 1").Value;
        order.Submit(shipping, DateTimeOffset.UtcNow.AddMinutes(-10));
        order.MarkAsPaid();

        var shippedAt = new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero);
        order.Ship("HCT Logistics", "HCT-9999", shippedAt);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ShipmentTracking);
        Assert.Equal("HCT Logistics", result.Value.ShipmentTracking!.Carrier);
        Assert.Equal("HCT-9999", result.Value.ShipmentTracking.TrackingNumber);
        Assert.Equal(shippedAt, result.Value.ShipmentTracking.ShippedAt);
    }

    [Fact]
    public async Task Handle_WhenOrderIsHistoricalShippedWithoutTracking_ReturnsNullShipmentTracking()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.ChangeStatus(OrderStatus.Submitted);
        order.ChangeStatus(OrderStatus.Paid);
        order.Ship(); // Historical ship without tracking fields

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Shipped", result.Value.Status);
        Assert.Null(result.Value.ShipmentTracking);
    }
}
