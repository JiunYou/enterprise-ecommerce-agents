using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class OrderFulfillmentMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public OrderFulfillmentMySqlAcceptanceTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContextOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, options => { });
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private EnterpriseCommerceDbContext CreateFreshDbContext() => new(_dbContextOptions);

    [Fact]
    public async Task FulfillmentQueue_RealMySql_ReturnsOnlyPaidOrders_DeterministicOrdering_AndHandlesHistoricalNullAddress()
    {
        var testRunPrefix = Guid.NewGuid().ToString("N")[..8];
        var customerId = Guid.NewGuid();

        // 1. 建立各狀態之訂單
        // A. 歷史 Paid 訂單（無 ShippingAddress） - 提交時間最早: 08:00
        var historicalPaidOrder = Order.Create(customerId, "TWD");
        historicalPaidOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        historicalPaidOrder.ChangeStatus(OrderStatus.Submitted);
        // 設定 SubmittedAt 反映歷史時間
        typeof(Order).GetProperty(nameof(Order.SubmittedAt))!
            .SetValue(historicalPaidOrder, new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero));
        historicalPaidOrder.MarkAsPaid();

        // B. 正常 Paid 訂單 1 - 提交時間: 10:00
        var paidOrder1 = Order.Create(customerId, "TWD");
        paidOrder1.AddItem(new ProductId(Guid.NewGuid()), new Money(200m, "TWD"), 2);
        var address1 = ShippingAddress.Create(
            $"Recipient {testRunPrefix} 1",
            "+886912345671",
            "TW",
            "100",
            "Taipei",
            "Fulfillment St 1",
            "Floor 1").Value;
        paidOrder1.Submit(address1, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero));
        paidOrder1.MarkAsPaid();

        // C. 正常 Paid 訂單 2 - 提交時間最晚: 12:00
        var paidOrder2 = Order.Create(customerId, "TWD");
        paidOrder2.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        var address2 = ShippingAddress.Create(
            $"Recipient {testRunPrefix} 2",
            "+886912345672",
            "TW",
            "100",
            "Taipei",
            "Fulfillment St 2",
            null).Value;
        paidOrder2.Submit(address2, new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        paidOrder2.MarkAsPaid();

        // D. Submitted 訂單 - 不應出現在履約佇列中
        var submittedOrder = Order.Create(customerId, "TWD");
        submittedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(150m, "TWD"), 1);
        var addressSubmitted = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Submitted",
            "+886912345673",
            "TW",
            "100",
            "Taipei",
            "Submitted St",
            null).Value;
        submittedOrder.Submit(addressSubmitted, new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero));

        // E. Shipped 訂單 - 不應出現在履約佇列中
        var shippedOrder = Order.Create(customerId, "TWD");
        shippedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(250m, "TWD"), 1);
        var addressShipped = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Shipped",
            "+886912345674",
            "TW",
            "100",
            "Taipei",
            "Shipped St",
            null).Value;
        shippedOrder.Submit(addressShipped, new DateTimeOffset(2026, 9, 1, 7, 0, 0, TimeSpan.Zero));
        shippedOrder.MarkAsPaid();
        shippedOrder.Ship();

        // F. Cancelled 訂單 - 不應出現在履約佇列中
        var cancelledOrder = Order.Create(customerId, "TWD");
        cancelledOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(50m, "TWD"), 1);
        cancelledOrder.Cancel();

        // 寫入真實 MySQL 資料庫
        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.AddRange(historicalPaidOrder, paidOrder1, paidOrder2, submittedOrder, shippedOrder, cancelledOrder);
            await dbContext.SaveChangesAsync();
        }

        // 2. 測試 Repository 層實體查詢
        await using (var dbContext = CreateFreshDbContext())
        {
            var repository = new OrderRepository(dbContext);
            var queue = await repository.GetFulfillmentQueueAsync(limit: 50);

            // 必須包含三筆 Paid 訂單，完全排除 Submitted, Shipped, Cancelled
            var relevantOrders = queue.Where(o =>
                o.Id == historicalPaidOrder.Id ||
                o.Id == paidOrder1.Id ||
                o.Id == paidOrder2.Id ||
                o.Id == submittedOrder.Id ||
                o.Id == shippedOrder.Id ||
                o.Id == cancelledOrder.Id).ToList();

            relevantOrders.Should().HaveCount(3);
            relevantOrders.Select(o => o.Status).Should().AllBeEquivalentTo(OrderStatus.Paid);

            // 驗證排序為 SubmittedAt 升冪
            relevantOrders[0].Id.Should().Be(historicalPaidOrder.Id);
            relevantOrders[1].Id.Should().Be(paidOrder1.Id);
            relevantOrders[2].Id.Should().Be(paidOrder2.Id);

            // 驗證歷史訂單 ShippingAddress 為 null 不拋出例外
            relevantOrders[0].ShippingAddress.Should().BeNull();

            // 驗證 ShippingAddress 欄位完整載入
            relevantOrders[1].ShippingAddress.Should().NotBeNull();
            relevantOrders[1].ShippingAddress!.RecipientName.Should().Be($"Recipient {testRunPrefix} 1");
            relevantOrders[1].ShippingAddress!.AddressLine2.Should().Be("Floor 1");

            relevantOrders[2].ShippingAddress.Should().NotBeNull();
            relevantOrders[2].ShippingAddress!.RecipientName.Should().Be($"Recipient {testRunPrefix} 2");
            relevantOrders[2].ShippingAddress!.AddressLine2.Should().BeNull();
        }

        // 3. 測試 WebApi 端到端端點（使用 Admin 身份查詢 API）
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var response = await client.GetAsync("/api/v1/Orders/fulfillment?limit=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var apiOrders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        apiOrders.Should().NotBeNull();

        var apiRelevant = apiOrders!.Where(o =>
            o.Id == historicalPaidOrder.Id.Value ||
            o.Id == paidOrder1.Id.Value ||
            o.Id == paidOrder2.Id.Value ||
            o.Id == submittedOrder.Id.Value ||
            o.Id == shippedOrder.Id.Value ||
            o.Id == cancelledOrder.Id.Value).ToList();

        apiRelevant.Should().HaveCount(3);
        apiRelevant.Select(o => o.Status).Should().AllBeEquivalentTo("Paid");
        apiRelevant[0].Id.Should().Be(historicalPaidOrder.Id.Value);
        apiRelevant[0].ShippingAddress.Should().BeNull();
        apiRelevant[1].Id.Should().Be(paidOrder1.Id.Value);
        apiRelevant[1].ShippingAddress.Should().NotBeNull();
        apiRelevant[1].ShippingAddress!.RecipientName.Should().Be($"Recipient {testRunPrefix} 1");

        // 4. 驗證真實發貨 (PUT /api/v1/Orders/{id}/ship) 變更狀態並將訂單從佇列移除
        var shipResponse = await client.PutAsync($"/api/v1/Orders/{paidOrder1.Id.Value}/ship", null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 重新查詢佇列，paidOrder1 應已被移出佇列
        var afterShipResponse = await client.GetAsync("/api/v1/Orders/fulfillment?limit=50");
        afterShipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterShipOrders = await afterShipResponse.Content.ReadFromJsonAsync<List<OrderResponse>>();
        afterShipOrders.Should().NotBeNull();
        afterShipOrders!.Any(o => o.Id == paidOrder1.Id.Value).Should().BeFalse();

        // 驗證資料庫中 paidOrder1 狀態確實為 Shipped
        await using (var dbContext = CreateFreshDbContext())
        {
            var dbOrder = await dbContext.Orders.FirstAsync(o => o.Id == paidOrder1.Id);
            dbOrder.Status.Should().Be(OrderStatus.Shipped);
        }
    }
}
