using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class CustomerOrderHistoryAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CustomerOrderHistoryAcceptanceTests(MySqlFixture mySqlFixture)
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

    private HttpClient CreateCustomerClient(Guid? customerId = null, bool anonymous = false)
    {
        var client = _factory.CreateClient();
        if (!anonymous)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
            if (customerId.HasValue)
            {
                client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.Value.ToString());
            }
        }
        return client;
    }

    [Fact]
    public async Task GetOrders_WhenAnonymous_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = CreateCustomerClient(anonymous: true);

        // Act
        var response = await client.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrders_WhenAuthenticatedWithoutCustomerIdClaim_ShouldReturn403Forbidden()
    {
        // Arrange: 具備 TestScheme Authorization 但無 X-Test-User-Id Claim
        var client = CreateCustomerClient(customerId: null, anonymous: false);

        // Act
        var response = await client.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOrders_WhenAuthenticated_ShouldEnforceMembershipRules_DataIsolation_Ordering_AndFrozenPayload()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var address = ShippingAddress.Create("Alice", "0912345678", "TW", "100", "Taipei", "Main St 1").Value;

        var baseTime = DateTimeOffset.UtcNow;

        // 1. Customer A: 已送出訂單 1 (Submitted, T - 3h, 100 TWD)
        var orderA1 = Order.Create(customerA, "TWD");
        orderA1.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        orderA1.Submit(address, baseTime.AddHours(-3));

        // 2. Customer A: 已送出且已付款訂單 2 (Paid, T - 2h, 250 TWD)
        var orderA2 = Order.Create(customerA, "TWD");
        orderA2.AddItem(new ProductId(Guid.NewGuid()), new Money(250m, "TWD"), 1);
        orderA2.Submit(address, baseTime.AddHours(-2));
        orderA2.MarkAsPaid();

        // 3. Customer A: 已送出且後續取消之訂單 3 (Submitted + Cancelled, T - 1h, 300 TWD) -> 必須在歷史列表中
        var orderA3 = Order.Create(customerA, "TWD");
        orderA3.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        orderA3.Submit(address, baseTime.AddHours(-1));
        orderA3.Cancel();

        // 4. Customer A: 未送出之購物車草稿 (Pending, SubmittedAt == null) -> 必須排除！
        var orderA4Pending = Order.Create(customerA, "TWD");
        orderA4Pending.AddItem(new ProductId(Guid.NewGuid()), new Money(400m, "TWD"), 1);

        // 5. Customer A: 未送出即取消之購物車 (Cancelled, SubmittedAt == null) -> 必須排除！
        var orderA5CancelledUnsubmitted = Order.Create(customerA, "TWD");
        orderA5CancelledUnsubmitted.Cancel();

        // 6. Customer B: 已送出訂單 (Submitted, T - 30m, 999 TWD) -> 屬於 Customer B，Customer A 絕不可看到！
        var orderB = Order.Create(customerB, "TWD");
        orderB.AddItem(new ProductId(Guid.NewGuid()), new Money(999m, "TWD"), 1);
        orderB.Submit(address, baseTime.AddMinutes(-30));

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            dbContext.Orders.AddRange(orderA1, orderA2, orderA3, orderA4Pending, orderA5CancelledUnsubmitted, orderB);
            await dbContext.SaveChangesAsync();
        }

        var clientA = CreateCustomerClient(customerA);
        var clientB = CreateCustomerClient(customerB);

        // Act - 預設查詢 (Page 1, PageSize 25)
        var responseA = await clientA.GetAsync("/api/v1/orders");
        var responseB = await clientB.GetAsync("/api/v1/orders");

        // Assert - HTTP Status
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - Customer A 結果
        var pageA = await responseA.Content.ReadFromJsonAsync<CustomerOrderPageResponse>();
        pageA.Should().NotBeNull();
        pageA!.Page.Should().Be(1);
        pageA.PageSize.Should().Be(25);
        pageA.TotalCount.Should().Be(3); // 只有 orderA3, orderA2, orderA1

        var itemsA = pageA.Items;
        itemsA.Count.Should().Be(3);

        // 驗證排序 (SubmittedAt descending)
        itemsA[0].Id.Should().Be(orderA3.Id.Value);
        itemsA[0].Status.Should().Be(OrderStatus.Cancelled.ToString());
        itemsA[0].TotalAmount.Should().Be(300m);
        itemsA[0].Currency.Should().Be("TWD");

        itemsA[1].Id.Should().Be(orderA2.Id.Value);
        itemsA[1].Status.Should().Be(OrderStatus.Paid.ToString());
        itemsA[1].TotalAmount.Should().Be(250m);
        itemsA[1].Currency.Should().Be("TWD");

        itemsA[2].Id.Should().Be(orderA1.Id.Value);
        itemsA[2].Status.Should().Be(OrderStatus.Submitted.ToString());
        itemsA[2].TotalAmount.Should().Be(100m);
        itemsA[2].Currency.Should().Be("TWD");

        // 驗證未送出的 Pending 訂單與未送出的 Cancelled 訂單均被排除
        itemsA.Select(x => x.Id).Should().NotContain(orderA4Pending.Id.Value);
        itemsA.Select(x => x.Id).Should().NotContain(orderA5CancelledUnsubmitted.Id.Value);

        // 驗證資料隔離：Customer B 的訂單絕不流向 Customer A
        itemsA.Select(x => x.Id).Should().NotContain(orderB.Id.Value);

        // Assert - Customer B 結果 (只有 orderB)
        var pageB = await responseB.Content.ReadFromJsonAsync<CustomerOrderPageResponse>();
        pageB.Should().NotBeNull();
        pageB!.Page.Should().Be(1);
        pageB.PageSize.Should().Be(25);
        pageB.TotalCount.Should().Be(1);
        pageB.Items.Count.Should().Be(1);
        pageB.Items[0].Id.Should().Be(orderB.Id.Value);
        pageB.Items[0].TotalAmount.Should().Be(999m);

        // Assert - Frozen Payload 欄位驗證 (不洩漏 CustomerId 或其他內部資訊)
        var rawJson = await responseA.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var root = jsonDoc.RootElement;
        var rootProperties = root.EnumerateObject().Select(p => p.Name).ToList();
        rootProperties.Should().BeEquivalentTo(new[] { "items", "page", "pageSize", "totalCount" });

        var firstElement = root.GetProperty("items")[0];

        // 確保精確只包含 5 個允許欄位：id, status, submittedAt, totalAmount, currency
        var itemProperties = firstElement.EnumerateObject().Select(p => p.Name).ToList();
        itemProperties.Should().BeEquivalentTo(new[] { "id", "status", "submittedAt", "totalAmount", "currency" });

        // 明確確保禁止欄位不存在
        itemProperties.Should().NotContain("customerId");
        itemProperties.Should().NotContain("version");
        itemProperties.Should().NotContain("items");
        itemProperties.Should().NotContain("shippingAddress");
        itemProperties.Should().NotContain("paymentAttemptId");
        itemProperties.Should().NotContain("provider");
    }

    [Fact]
    public async Task GetOrders_WhenAuthenticated_MultiplePages_ShouldSlicePagesDeterministically_WithNoDuplicates()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var address = ShippingAddress.Create("Alice", "0912345678", "TW", "100", "Taipei", "Main St 1").Value;
        var baseTime = DateTimeOffset.UtcNow;

        var order1 = Order.Create(customerA, "TWD");
        order1.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order1.Submit(address, baseTime.AddHours(-3));

        var order2 = Order.Create(customerA, "TWD");
        order2.AddItem(new ProductId(Guid.NewGuid()), new Money(200m, "TWD"), 1);
        order2.Submit(address, baseTime.AddHours(-2));

        var order3 = Order.Create(customerA, "TWD");
        order3.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        order3.Submit(address, baseTime.AddHours(-1));

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            dbContext.Orders.AddRange(order1, order2, order3);
            await dbContext.SaveChangesAsync();
        }

        var clientA = CreateCustomerClient(customerA);

        // Act - Page 1 (pageSize = 2)
        var responsePage1 = await clientA.GetAsync("/api/v1/orders?page=1&pageSize=2");
        var responsePage2 = await clientA.GetAsync("/api/v1/orders?page=2&pageSize=2");

        // Assert
        responsePage1.StatusCode.Should().Be(HttpStatusCode.OK);
        responsePage2.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1 = await responsePage1.Content.ReadFromJsonAsync<CustomerOrderPageResponse>();
        var page2 = await responsePage2.Content.ReadFromJsonAsync<CustomerOrderPageResponse>();

        page1.Should().NotBeNull();
        page2.Should().NotBeNull();

        page1!.Page.Should().Be(1);
        page1.PageSize.Should().Be(2);
        page1.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page1.Items[0].Id.Should().Be(order3.Id.Value);
        page1.Items[1].Id.Should().Be(order2.Id.Value);

        page2!.Page.Should().Be(2);
        page2.PageSize.Should().Be(2);
        page2.TotalCount.Should().Be(3);
        page2.Items.Should().HaveCount(1);
        page2.Items[0].Id.Should().Be(order1.Id.Value);

        // 驗證跨頁絕無重複 (No duplicates across pages)
        var page1Ids = page1.Items.Select(x => x.Id).ToList();
        var page2Ids = page2.Items.Select(x => x.Id).ToList();
        page1Ids.Intersect(page2Ids).Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrders_WhenAuthenticated_OutOfRangePage_ShouldReturnEmptyItems_PreserveTotalCount_AndEchoRequestedPage()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var address = ShippingAddress.Create("Alice", "0912345678", "TW", "100", "Taipei", "Main St 1").Value;

        var order1 = Order.Create(customerA, "TWD");
        order1.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order1.Submit(address, DateTimeOffset.UtcNow);

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            dbContext.Orders.Add(order1);
            await dbContext.SaveChangesAsync();
        }

        var clientA = CreateCustomerClient(customerA);

        // Act - 大頁碼 (超出資料總頁數)
        var response = await clientA.GetAsync("/api/v1/orders?page=99&pageSize=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<CustomerOrderPageResponse>();
        page.Should().NotBeNull();
        page!.Page.Should().Be(99); // Echo requested page
        page.PageSize.Should().Be(25);
        page.TotalCount.Should().Be(1);
        page.Items.Should().BeEmpty();
    }
}
