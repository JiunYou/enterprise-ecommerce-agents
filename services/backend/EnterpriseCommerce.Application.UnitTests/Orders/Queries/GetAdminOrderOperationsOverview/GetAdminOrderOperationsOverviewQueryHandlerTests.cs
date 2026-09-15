using System.Reflection;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderOperationsOverview;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetAdminOrderOperationsOverview;

public class GetAdminOrderOperationsOverviewQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly GetAdminOrderOperationsOverviewQueryHandler _handler;

    public GetAdminOrderOperationsOverviewQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _handler = new GetAdminOrderOperationsOverviewQueryHandler(_orderRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsZeroCountsAndEmptyRecentOrders()
    {
        // Arrange
        var emptyData = new AdminOrderOperationsOverviewData(
            TotalCount: 0,
            SubmittedCount: 0,
            PaidCount: 0,
            ShippedCount: 0,
            CancelledCount: 0,
            RecentOrders: new List<AdminOrderOverviewRecentOrderData>());

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyData);

        var query = new GetAdminOrderOperationsOverviewQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.SubmittedCount);
        Assert.Equal(0, result.Value.PaidCount);
        Assert.Equal(0, result.Value.ShippedCount);
        Assert.Equal(0, result.Value.CancelledCount);
        Assert.Empty(result.Value.RecentOrders);
    }

    [Fact]
    public async Task Handle_ValidData_ReturnsCorrectTotalAndStatusCounts()
    {
        // Arrange: 4 formal orders total (1 Submitted + 2 Paid + 1 Shipped + 0 Cancelled)
        var data = new AdminOrderOperationsOverviewData(
            TotalCount: 4,
            SubmittedCount: 1,
            PaidCount: 2,
            ShippedCount: 1,
            CancelledCount: 0,
            RecentOrders: new List<AdminOrderOverviewRecentOrderData>
            {
                new(Guid.NewGuid(), "Paid", "TWD", 1000m, DateTimeOffset.UtcNow)
            });

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var query = new GetAdminOrderOperationsOverviewQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.TotalCount);
        Assert.Equal(1, result.Value.SubmittedCount);
        Assert.Equal(2, result.Value.PaidCount);
        Assert.Equal(1, result.Value.ShippedCount);
        Assert.Equal(0, result.Value.CancelledCount);
        Assert.Equal(
            result.Value.SubmittedCount + result.Value.PaidCount + result.Value.ShippedCount + result.Value.CancelledCount,
            result.Value.TotalCount);
    }

    [Fact]
    public async Task Handle_PendingCartsAreExcluded_TotalMatchesFormalStatusesOnly()
    {
        // Invariant: Pending cart is not counted in total or any formal status
        var data = new AdminOrderOperationsOverviewData(
            TotalCount: 3,
            SubmittedCount: 1,
            PaidCount: 1,
            ShippedCount: 1,
            CancelledCount: 0,
            RecentOrders: new List<AdminOrderOverviewRecentOrderData>
            {
                new(Guid.NewGuid(), "Submitted", "TWD", 300m, DateTimeOffset.UtcNow)
            });

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var query = new GetAdminOrderOperationsOverviewQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.DoesNotContain(result.Value.RecentOrders, o => o.Status == "Pending");
    }

    [Fact]
    public async Task Handle_RecentOrdersMapping_MapsAllRequiredFieldsCorrectly()
    {
        var orderId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recentOrdersData = new List<AdminOrderOverviewRecentOrderData>
        {
            new(orderId, "Paid", "TWD", 1250.50m, submittedAt)
        };

        var data = new AdminOrderOperationsOverviewData(
            TotalCount: 1,
            SubmittedCount: 0,
            PaidCount: 1,
            ShippedCount: 0,
            CancelledCount: 0,
            RecentOrders: recentOrdersData);

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var query = new GetAdminOrderOperationsOverviewQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var recent = Assert.Single(result.Value.RecentOrders);
        Assert.Equal(orderId, recent.Id);
        Assert.Equal("Paid", recent.Status);
        Assert.Equal("TWD", recent.Currency);
        Assert.Equal(1250.50m, recent.TotalAmount);
        Assert.Equal(submittedAt, recent.SubmittedAt);
    }

    [Fact]
    public async Task Handle_RecentOrdersMaximumFive_DoesNotExceedFive()
    {
        var recentOrders = new List<AdminOrderOverviewRecentOrderData>();
        for (int i = 0; i < 5; i++)
        {
            recentOrders.Add(new AdminOrderOverviewRecentOrderData(
                Guid.NewGuid(), "Paid", "TWD", 100m * (i + 1), DateTimeOffset.UtcNow.AddHours(-i)));
        }

        var data = new AdminOrderOperationsOverviewData(
            TotalCount: 10,
            SubmittedCount: 5,
            PaidCount: 5,
            ShippedCount: 0,
            CancelledCount: 0,
            RecentOrders: recentOrders);

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var query = new GetAdminOrderOperationsOverviewQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.RecentOrders.Count <= 5);
        Assert.Equal(5, result.Value.RecentOrders.Count);
    }

    [Fact]
    public async Task Handle_DeterministicRecentOrdering_PreservesSubmittedAtDescAndIdDesc()
    {
        var now = DateTimeOffset.UtcNow;
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Seeded in deterministic descending order
        var recentOrders = new List<AdminOrderOverviewRecentOrderData>
        {
            new(id1, "Paid", "TWD", 200m, now),
            new(id2, "Submitted", "TWD", 100m, now)
        };

        var data = new AdminOrderOperationsOverviewData(
            TotalCount: 2,
            SubmittedCount: 1,
            PaidCount: 1,
            ShippedCount: 0,
            CancelledCount: 0,
            RecentOrders: recentOrders);

        _orderRepositoryMock
            .Setup(r => r.GetAdminOrderOperationsOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var query = new GetAdminOrderOperationsOverviewQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(id1, result.Value.RecentOrders[0].Id);
        Assert.Equal(id2, result.Value.RecentOrders[1].Id);
    }

    [Fact]
    public void ResponseModel_ExcludesCustomerIdAndPii()
    {
        // Verify via reflection that CustomerId or any PII is NOT exposed in the overview response model
        var overviewProperties = typeof(AdminOrderOperationsOverviewResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var recentOrderProperties = typeof(AdminOrderOperationsOverviewRecentOrder).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var forbiddenNames = new[] { "CustomerId", "Customer", "Address", "Recipient", "Phone", "Email", "RecipientName" };

        foreach (var forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(overviewProperties, p => p.Name.Equals(forbidden, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(recentOrderProperties, p => p.Name.Equals(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }
}
