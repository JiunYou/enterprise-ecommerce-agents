using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetAdminOrderById;

public class GetAdminOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetAdminOrderByIdQueryHandler _handler;

    public GetAdminOrderByIdQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetAdminOrderByIdQueryHandler(_orderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingOrderWithItemsAndAddress_MapsAllFieldsCorrectly()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = new ProductId(Guid.NewGuid());
        var unitPrice = new Money(250m, "TWD");
        order.AddItem(productId, unitPrice, 2);

        var address = ShippingAddress.Create(
            "Alice Chen",
            "+886912345678",
            "TW",
            "100",
            "Taipei",
            "Zhongxiao E Rd",
            "5F").Value;
        var submittedAt = new DateTimeOffset(2026, 9, 2, 14, 30, 0, TimeSpan.Zero);
        order.Submit(address, submittedAt);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetAdminOrderByIdQuery(order.Id.Value);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var detail = result.Value;
        Assert.Equal(order.Id.Value, detail.Id);
        Assert.Equal(customerId, detail.CustomerId);
        Assert.Equal("Submitted", detail.Status);
        Assert.Equal("TWD", detail.Currency);
        Assert.Equal(500m, detail.TotalAmount);
        Assert.Equal(submittedAt, detail.SubmittedAt);

        // Verify items
        Assert.Single(detail.Items);
        var item = detail.Items.First();
        Assert.Equal(productId.Value, item.ProductId);
        Assert.Equal(250m, item.UnitPrice);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(500m, item.TotalPrice);

        // Verify ShippingAddress
        Assert.NotNull(detail.ShippingAddress);
        Assert.Equal("Alice Chen", detail.ShippingAddress!.RecipientName);
        Assert.Equal("+886912345678", detail.ShippingAddress.Phone);
        Assert.Equal("TW", detail.ShippingAddress.CountryCode);
        Assert.Equal("100", detail.ShippingAddress.PostalCode);
        Assert.Equal("Taipei", detail.ShippingAddress.City);
        Assert.Equal("Zhongxiao E Rd", detail.ShippingAddress.AddressLine1);
        Assert.Equal("5F", detail.ShippingAddress.AddressLine2);
    }

    [Fact]
    public async Task Handle_HistoricalOrderWithNullShippingAddress_ReturnsSafelyWithNullAddress()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "USD"), 1);
        order.ChangeStatus(OrderStatus.Submitted);

        Assert.Null(order.ShippingAddress);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetAdminOrderByIdQuery(order.Id.Value);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Id.Value, result.Value.Id);
        Assert.Null(result.Value.ShippingAddress);
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var missingOrderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(missingOrderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var query = new GetAdminOrderByIdQuery(missingOrderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
    }
}
