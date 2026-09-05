using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetAdminOrders;

public class GetAdminOrdersQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetAdminOrdersQueryHandler _handler;

    public GetAdminOrdersQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetAdminOrdersQueryHandler(_orderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_DefaultPageAndPageSize_PassedCorrectlyToRepository()
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(25, result.Value.PageSize);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            null,
            null,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Handle_PageLessThanOrEqualToZero_NormalizesToOne(int invalidPage)
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(Page: invalidPage);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Page);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            null,
            null,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(-50)]
    public async Task Handle_PageSizeLessThanOrEqualToZero_NormalizesToTwentyFive(int invalidPageSize)
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(PageSize: invalidPageSize);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.PageSize);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            null,
            null,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    [InlineData(1000)]
    public async Task Handle_PageSizeGreaterThanOneHundred_CappedToOneHundred(int excessivePageSize)
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(PageSize: excessivePageSize);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.PageSize);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            null,
            null,
            1,
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Pending", OrderStatus.Pending)]
    [InlineData("submitted", OrderStatus.Submitted)]
    [InlineData("PAID", OrderStatus.Paid)]
    [InlineData("Shipped", OrderStatus.Shipped)]
    [InlineData("cancelled", OrderStatus.Cancelled)]
    public async Task Handle_ValidStatusFilter_PassedCorrectlyCaseInsensitive(string statusInput, OrderStatus expectedStatus)
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                expectedStatus,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(Status: statusInput);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            expectedStatus,
            null,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("Unknown")]
    [InlineData("123")]
    public async Task Handle_InvalidStatusFilter_ReturnsFailureWithoutCallingRepository(string invalidStatus)
    {
        // Arrange
        var query = new GetAdminOrdersQuery(Status: invalidStatus);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("AdminOrders.InvalidStatus", result.Error.Code);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            It.IsAny<OrderStatus?>(),
            It.IsAny<OrderId?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OrderIdFilter_PassedCorrectlyToRepository()
    {
        // Arrange
        var targetOrderId = Guid.NewGuid();
        var expectedDomainOrderId = new OrderId(targetOrderId);

        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                expectedDomainOrderId,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(OrderId: targetOrderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            null,
            expectedDomainOrderId,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CombinedStatusAndOrderIdFilter_PassesBothToRepository()
    {
        // Arrange
        var targetOrderId = Guid.NewGuid();
        var expectedDomainOrderId = new OrderId(targetOrderId);

        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                OrderStatus.Paid,
                expectedDomainOrderId,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order>(), 0));

        var query = new GetAdminOrdersQuery(Status: "Paid", OrderId: targetOrderId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _orderRepositoryMock.Verify(r => r.GetAdminOrdersAsync(
            OrderStatus.Paid,
            expectedDomainOrderId,
            1,
            25,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsMappedSummaries_AndExcludesShippingAddressAndItems()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(150m, "TWD"), 2);
        var address = ShippingAddress.Create(
            "Secret Name", "+886900000000", "TW", "100", "Taipei", "Secret Rd", null).Value;
        var submittedAt = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        order.Submit(address, submittedAt);

        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order> { order }, 1));

        var query = new GetAdminOrdersQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Single(result.Value.Items);

        var summary = result.Value.Items[0];
        Assert.Equal(order.Id.Value, summary.Id);
        Assert.Equal(customerId, summary.CustomerId);
        Assert.Equal("Submitted", summary.Status);
        Assert.Equal("TWD", summary.Currency);
        Assert.Equal(300m, summary.TotalAmount);
        Assert.Equal(submittedAt, summary.SubmittedAt);

        // Verify contract does not leak items or address
        var propertyNames = typeof(AdminOrderSummaryResponse).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Items", propertyNames);
        Assert.DoesNotContain("ShippingAddress", propertyNames);
        Assert.DoesNotContain("AddressLine1", propertyNames);
        Assert.DoesNotContain("RecipientName", propertyNames);
    }

    [Fact]
    public async Task Handle_PendingOrderWithNullSubmittedAt_MapsSafely()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var pendingOrder = Order.Create(customerId, "USD");

        _orderRepositoryMock.Setup(r => r.GetAdminOrdersAsync(
                null,
                null,
                1,
                25,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Order> { pendingOrder }, 1));

        var query = new GetAdminOrdersQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        var summary = result.Value.Items[0];
        Assert.Equal(pendingOrder.Id.Value, summary.Id);
        Assert.Equal("Pending", summary.Status);
        Assert.Null(summary.SubmittedAt);
    }
}
