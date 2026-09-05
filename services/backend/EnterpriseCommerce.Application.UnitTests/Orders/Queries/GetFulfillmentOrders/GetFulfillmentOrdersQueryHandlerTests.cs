using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetFulfillmentOrders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetFulfillmentOrders;

public class GetFulfillmentOrdersQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetFulfillmentOrdersQueryHandler _handler;

    public GetFulfillmentOrdersQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetFulfillmentOrdersQueryHandler(_orderRepositoryMock.Object);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-10, 50)]
    [InlineData(25, 25)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public async Task Handle_EnforcesLimitBoundariesCorrectly(int requestedLimit, int expectedLimitPassedToRepository)
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetFulfillmentQueueAsync(expectedLimitPassedToRepository, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        var query = new GetFulfillmentOrdersQuery(requestedLimit);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _orderRepositoryMock.Verify(r => r.GetFulfillmentQueueAsync(expectedLimitPassedToRepository, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaidOrdersExist_ReturnsMappedOrderResponses()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = new ProductId(Guid.NewGuid());
        var unitPrice = new Money(150m, "TWD");
        order.AddItem(productId, unitPrice, 2);

        var shippingAddress = ShippingAddress.Create(
            "Jane Doe",
            "+886912345678",
            "TW",
            "100",
            "Taipei",
            "Section 1, Main St",
            "Floor 2").Value;

        var submitResult = order.Submit(shippingAddress, DateTimeOffset.UtcNow);
        Assert.True(submitResult.IsSuccess);

        var payResult = order.MarkAsPaid();
        Assert.True(payResult.IsSuccess);

        _orderRepositoryMock.Setup(r => r.GetFulfillmentQueueAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        var query = new GetFulfillmentOrdersQuery(50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);

        var response = result.Value.First();
        Assert.Equal(order.Id.Value, response.Id);
        Assert.Equal(customerId, response.CustomerId);
        Assert.Equal(OrderStatus.Paid.ToString(), response.Status);
        Assert.Equal("TWD", response.Currency);
        Assert.Equal(300m, response.TotalAmount);
        Assert.Single(response.Items);

        var item = response.Items.First();
        Assert.Equal(productId.Value, item.ProductId);
        Assert.Equal(150m, item.UnitPrice);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(300m, item.TotalPrice);

        Assert.NotNull(response.ShippingAddress);
        Assert.Equal("Jane Doe", response.ShippingAddress!.RecipientName);
        Assert.Equal("+886912345678", response.ShippingAddress.Phone);
        Assert.Equal("TW", response.ShippingAddress.CountryCode);
        Assert.Equal("100", response.ShippingAddress.PostalCode);
        Assert.Equal("Taipei", response.ShippingAddress.City);
        Assert.Equal("Section 1, Main St", response.ShippingAddress.AddressLine1);
        Assert.Equal("Floor 2", response.ShippingAddress.AddressLine2);
    }

    [Fact]
    public async Task Handle_WhenHistoricalPaidOrderHasNullShippingAddress_ReturnsNullWithoutThrowing()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        var unitPrice = new Money(50m, "USD");
        order.AddItem(productId, unitPrice, 1);

        // 模擬在 ShippingAddress 引入前的歷史訂單（直接變更為 Submitted 並 MarkAsPaid）
        var submitResult = order.ChangeStatus(OrderStatus.Submitted);
        Assert.True(submitResult.IsSuccess);
        var payResult = order.MarkAsPaid();
        Assert.True(payResult.IsSuccess);

        Assert.Null(order.ShippingAddress);

        _orderRepositoryMock.Setup(r => r.GetFulfillmentQueueAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        var query = new GetFulfillmentOrdersQuery(50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);

        var response = result.Value.First();
        Assert.Equal(order.Id.Value, response.Id);
        Assert.Equal(OrderStatus.Paid.ToString(), response.Status);
        Assert.Null(response.ShippingAddress);
    }

    [Fact]
    public async Task Handle_WhenNoOrdersMatch_ReturnsEmptyList()
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetFulfillmentQueueAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        var query = new GetFulfillmentOrdersQuery(50);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }
}
