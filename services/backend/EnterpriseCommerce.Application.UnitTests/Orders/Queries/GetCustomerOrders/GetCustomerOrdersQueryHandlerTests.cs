using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetCustomerOrders;

public class GetCustomerOrdersQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetCustomerOrdersQueryHandler _handler;

    public GetCustomerOrdersQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetCustomerOrdersQueryHandler(_orderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasOrders_ShouldForwardTrustedCustomerIdAndReturnSummaryList()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order1 = Order.Create(customerId, "TWD");
        var productId1 = new ProductId(Guid.NewGuid());
        order1.AddItem(productId1, new Money(150m, "TWD"), 2);
        var submittedAt1 = DateTimeOffset.UtcNow.AddHours(-2);
        var shippingAddress1 = ShippingAddress.Create("Alice", "0912345678", "TW", "100", "Taipei", "Line 1").Value;
        order1.Submit(shippingAddress1, submittedAt1);

        var order2 = Order.Create(customerId, "TWD");
        var productId2 = new ProductId(Guid.NewGuid());
        order2.AddItem(productId2, new Money(500m, "TWD"), 1);
        var submittedAt2 = DateTimeOffset.UtcNow.AddHours(-1);
        var shippingAddress2 = ShippingAddress.Create("Alice", "0912345678", "TW", "100", "Taipei", "Line 1").Value;
        order2.Submit(shippingAddress2, submittedAt2);
        order2.MarkAsPaid();

        var orders = new List<Order> { order2, order1 }; // 模擬 repository 已按 SubmittedAt 遞減排序

        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var query = new GetCustomerOrdersQuery(customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);

        // Verify order 2 (Paid, 500 TWD)
        var first = result.Value[0];
        Assert.Equal(order2.Id.Value, first.Id);
        Assert.Equal(OrderStatus.Paid.ToString(), first.Status);
        Assert.Equal(submittedAt2, first.SubmittedAt);
        Assert.Equal(500m, first.TotalAmount);
        Assert.Equal("TWD", first.Currency);

        // Verify order 1 (Submitted, 300 TWD)
        var second = result.Value[1];
        Assert.Equal(order1.Id.Value, second.Id);
        Assert.Equal(OrderStatus.Submitted.ToString(), second.Status);
        Assert.Equal(submittedAt1, second.SubmittedAt);
        Assert.Equal(300m, second.TotalAmount);
        Assert.Equal("TWD", second.Currency);

        // Verify repository interaction
        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasNoOrders_ShouldReturnSuccessWithEmptyList()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        var query = new GetCustomerOrdersQuery(customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);

        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
