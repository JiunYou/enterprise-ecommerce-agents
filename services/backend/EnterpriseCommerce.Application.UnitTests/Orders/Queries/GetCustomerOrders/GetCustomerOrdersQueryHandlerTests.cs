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
    public async Task Handle_WithDefaultParameters_ShouldNormalizeToPage1PageSize25_AndReturnMetadataAndSummaries()
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

        var orders = new List<Order> { order2, order1 };
        var totalCount = 42;

        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((orders, totalCount));

        var query = new GetCustomerOrdersQuery(customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(25, result.Value.PageSize);
        Assert.Equal(42, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);

        // Verify order 2 summary mapping (Paid, 500 TWD)
        var first = result.Value.Items[0];
        Assert.Equal(order2.Id.Value, first.Id);
        Assert.Equal(OrderStatus.Paid.ToString(), first.Status);
        Assert.Equal(submittedAt2, first.SubmittedAt);
        Assert.Equal(500m, first.TotalAmount);
        Assert.Equal("TWD", first.Currency);

        // Verify order 1 summary mapping (Submitted, 300 TWD)
        var second = result.Value.Items[1];
        Assert.Equal(order1.Id.Value, second.Id);
        Assert.Equal(OrderStatus.Submitted.ToString(), second.Status);
        Assert.Equal(submittedAt1, second.SubmittedAt);
        Assert.Equal(300m, second.TotalAmount);
        Assert.Equal("TWD", second.Currency);

        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, 1, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0, 0, 1, 25)]
    [InlineData(-5, -10, 1, 25)]
    [InlineData(-1, 20, 1, 20)]
    [InlineData(2, -1, 2, 25)]
    public async Task Handle_WithInvalidLowValues_ShouldNormalizePageTo1AndPageSizeTo25(
        int inputPage,
        int inputPageSize,
        int expectedPage,
        int expectedPageSize)
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, expectedPage, expectedPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetCustomerOrdersQuery(customerId, inputPage, inputPageSize);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);

        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, expectedPage, expectedPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    public async Task Handle_WithExcessivePageSize_ShouldNormalizePageSizeTo100(
        int inputPageSize,
        int expectedPageSize)
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, 1, expectedPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetCustomerOrdersQuery(customerId, Page: 1, PageSize: inputPageSize);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);

        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, 1, expectedPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasNoOrders_ShouldReturnSuccessWithEmptyItemsAndZeroTotalCount()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _orderRepositoryMock
            .Setup(r => r.GetCustomerOrderHistoryAsync(customerId, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetCustomerOrdersQuery(customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(25, result.Value.PageSize);

        _orderRepositoryMock.Verify(
            r => r.GetCustomerOrderHistoryAsync(customerId, 1, 25, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
