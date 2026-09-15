using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class AdminOrderOverviewMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminOrderOverviewMySqlAcceptanceTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContextOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private EnterpriseCommerceDbContext CreateFreshDbContext() => new(_dbContextOptions);

    private static ShippingAddress CreateTestAddress(string label) =>
        ShippingAddress.Create(
            $"Recipient {label}",
            "+886912345678",
            "TW",
            "100",
            "Taipei",
            $"{label} Street",
            null).Value;

    [Fact]
    public async Task GetAdminOrderOperationsOverview_RealMySql_MeetsAllAcceptanceCriteria()
    {
        // 1. 清理舊資料確保隔離性
        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.RemoveRange(dbContext.Orders);
            await dbContext.SaveChangesAsync();
        }

        var customerId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

        // 2. 種子資料：剛好 1 Pending + 1 Submitted + 1 Paid + 1 Shipped + 1 Cancelled
        // A. 1 Pending cart (SubmittedAt = null)
        var pendingCart = Order.Create(customerId, "TWD");
        pendingCart.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 2);

        // B. 1 Submitted order
        var submittedOrder = Order.Create(customerId, "TWD");
        submittedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(200m, "TWD"), 1);
        submittedOrder.Submit(CreateTestAddress("Submitted"), baseTime.AddHours(1));

        // C. 1 Paid order
        var paidOrder = Order.Create(customerId, "TWD");
        paidOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        paidOrder.Submit(CreateTestAddress("Paid"), baseTime.AddHours(2));
        paidOrder.MarkAsPaid();

        // D. 1 Shipped order
        var shippedOrder = Order.Create(customerId, "TWD");
        shippedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(400m, "TWD"), 1);
        shippedOrder.Submit(CreateTestAddress("Shipped"), baseTime.AddHours(3));
        shippedOrder.MarkAsPaid();
        shippedOrder.Ship();

        // E. 1 Cancelled order
        var cancelledOrder = Order.Create(customerId, "TWD");
        cancelledOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(500m, "TWD"), 1);
        cancelledOrder.Submit(CreateTestAddress("Cancelled"), baseTime.AddHours(4));
        cancelledOrder.Cancel();

        // 額外增加 3 筆正式訂單（共 7 筆正式訂單），以驗證：
        // 1) recentOrders.Count <= 5
        // 2) SubmittedAt DESC
        // 3) Id DESC tie-break (在同一 SubmittedAt 時)
        var tieBreakerTime = baseTime.AddHours(10);
        
        var extraPaid1 = Order.Create(customerId, "TWD");
        extraPaid1.AddItem(new ProductId(Guid.NewGuid()), new Money(50m, "TWD"), 1);
        extraPaid1.Submit(CreateTestAddress("ExtraPaid1"), baseTime.AddHours(5));
        extraPaid1.MarkAsPaid();

        var extraPaid2 = Order.Create(customerId, "TWD");
        extraPaid2.AddItem(new ProductId(Guid.NewGuid()), new Money(60m, "TWD"), 1);
        extraPaid2.Submit(CreateTestAddress("ExtraPaid2"), tieBreakerTime);
        extraPaid2.MarkAsPaid();

        var extraPaid3 = Order.Create(customerId, "TWD");
        extraPaid3.AddItem(new ProductId(Guid.NewGuid()), new Money(70m, "TWD"), 1);
        extraPaid3.Submit(CreateTestAddress("ExtraPaid3"), tieBreakerTime);
        extraPaid3.MarkAsPaid();

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.AddRange(
                pendingCart,
                submittedOrder,
                paidOrder,
                shippedOrder,
                cancelledOrder,
                extraPaid1,
                extraPaid2,
                extraPaid3);
            await dbContext.SaveChangesAsync();
        }

        // 3. 執行 Overview Repository 查詢
        AdminOrderOperationsOverviewData overview;
        await using (var dbContext = CreateFreshDbContext())
        {
            var repository = new OrderRepository(dbContext);
            overview = await repository.GetAdminOrderOperationsOverviewAsync();
        }

        // 4. 驗證正式訂單計數與 Pending 排除
        // 總共 7 筆 formal orders (Submitted: 1, Paid: 4, Shipped: 1, Cancelled: 1)
        overview.TotalCount.Should().Be(7);
        overview.SubmittedCount.Should().Be(1);
        overview.PaidCount.Should().Be(4);
        overview.ShippedCount.Should().Be(1);
        overview.CancelledCount.Should().Be(1);

        (overview.SubmittedCount + overview.PaidCount + overview.ShippedCount + overview.CancelledCount)
            .Should().Be(overview.TotalCount);

        // 5. 驗證近期訂單限制與排序
        overview.RecentOrders.Should().HaveCount(5);

        // 最新的是 tieBreakerTime 的兩筆（extraPaid2, extraPaid3），按照 Id DESC 排序
        var topTwo = overview.RecentOrders.Take(2).ToList();
        topTwo[0].SubmittedAt.Should().Be(tieBreakerTime);
        topTwo[1].SubmittedAt.Should().Be(tieBreakerTime);
        topTwo[0].Id.CompareTo(topTwo[1].Id).Should().BePositive();

        // 緊隨其後的是 baseTime + 5 hours, baseTime + 4 hours, baseTime + 3 hours
        overview.RecentOrders[2].SubmittedAt.Should().Be(baseTime.AddHours(5));
        overview.RecentOrders[3].SubmittedAt.Should().Be(baseTime.AddHours(4));
        overview.RecentOrders[4].SubmittedAt.Should().Be(baseTime.AddHours(3));

        // 驗證未包含任何 Pending 購物車
        overview.RecentOrders.Should().NotContain(o => o.Id == (Guid)pendingCart.Id);
    }

    [Fact]
    public async Task GetAdminOrderOperationsOverview_RealMySql_DiscountedOrder_PreservesAuthoritativeTotalAmount()
    {
        // 1. 清理舊資料確保隔離性
        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.RemoveRange(dbContext.Orders);
            await dbContext.SaveChangesAsync();
        }

        var customerId = Guid.NewGuid();
        var submittedTime = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

        // 2. 建立具備 100 TWD 優惠券折扣的 1000 TWD 正式訂單
        var discountedOrder = Order.Create(customerId, "TWD");
        discountedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(1000m, "TWD"), 1);
        var applyCouponResult = discountedOrder.ApplyCoupon("SAVE100", new Money(100m, "TWD"), submittedTime.AddDays(7));
        applyCouponResult.IsSuccess.Should().BeTrue();

        var submitResult = discountedOrder.Submit(CreateTestAddress("Discounted"), submittedTime);
        submitResult.IsSuccess.Should().BeTrue();

        // 驗證領域模型本身的權威金額
        discountedOrder.SubtotalAmount.Amount.Should().Be(1000m);
        discountedOrder.DiscountAmount.Amount.Should().Be(100m);
        discountedOrder.TotalAmount.Amount.Should().Be(900m);

        // 3. 持久化至真實 MySQL 資料庫
        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.Add(discountedOrder);
            await dbContext.SaveChangesAsync();
        }

        // 4. 執行倉儲端唯讀營運總覽查詢
        AdminOrderOperationsOverviewData overview;
        await using (var dbContext = CreateFreshDbContext())
        {
            var repository = new OrderRepository(dbContext);
            overview = await repository.GetAdminOrderOperationsOverviewAsync();
        }

        // 5. 斷言 Overview 的 recentOrder 正確反映權威的折後應付金額 900 TWD
        overview.TotalCount.Should().Be(1);
        overview.SubmittedCount.Should().Be(1);
        overview.RecentOrders.Should().HaveCount(1);

        var recent = overview.RecentOrders[0];
        recent.Id.Should().Be((Guid)discountedOrder.Id);
        recent.Currency.Should().Be("TWD");
        recent.TotalAmount.Should().Be(900m);
        recent.Status.Should().Be("Submitted");
        recent.SubmittedAt.Should().Be(submittedTime);
    }
}
