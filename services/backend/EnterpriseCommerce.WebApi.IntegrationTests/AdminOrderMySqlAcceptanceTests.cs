using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;
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
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class AdminOrderMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminOrderMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    private HttpClient CreateAdminClient()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    [Fact]
    public async Task AdminOrders_RealMySql_MeetsAllAcceptanceCriteria()
    {
        var testRunPrefix = Guid.NewGuid().ToString("N")[..8];
        var customerId = Guid.NewGuid();

        // 1. 建立涵蓋所有 5 種生命週期狀態的測試訂單
        // A. Pending 訂單 (SubmittedAt = null, ShippingAddress = null)
        var pendingOrder = Order.Create(customerId, "TWD");
        pendingOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);

        // B. Submitted 訂單 (SubmittedAt = 2026-09-01 09:00:00Z)
        var submittedOrder = Order.Create(customerId, "TWD");
        submittedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(200m, "TWD"), 1);
        var addressSubmitted = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Sub",
            "+886912345671",
            "TW",
            "100",
            "Taipei",
            "Submitted Rd",
            null).Value;
        var submittedAtTime = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        submittedOrder.Submit(addressSubmitted, submittedAtTime);

        // C. Paid 訂單 1 (SubmittedAt = 2026-09-01 10:00:00Z)
        var paidOrder1 = Order.Create(customerId, "TWD");
        paidOrder1.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        var addressPaid1 = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Paid1",
            "+886912345672",
            "TW",
            "100",
            "Taipei",
            "Paid St 1",
            null).Value;
        var paidAtTime1 = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
        paidOrder1.Submit(addressPaid1, paidAtTime1);
        paidOrder1.MarkAsPaid();

        // D. Paid 訂單 2 (SubmittedAt = 2026-09-01 10:30:00Z)
        var paidOrder2 = Order.Create(customerId, "TWD");
        paidOrder2.AddItem(new ProductId(Guid.NewGuid()), new Money(350m, "TWD"), 1);
        var addressPaid2 = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Paid2",
            "+886912345673",
            "TW",
            "100",
            "Taipei",
            "Paid St 2",
            null).Value;
        var paidAtTime2 = new DateTimeOffset(2026, 9, 1, 10, 30, 0, TimeSpan.Zero);
        paidOrder2.Submit(addressPaid2, paidAtTime2);
        paidOrder2.MarkAsPaid();

        // E. 歷史 Shipped 訂單 (ShippingAddress = null, SubmittedAt = 2026-09-01 11:00:00Z)
        var historicalShippedOrder = Order.Create(customerId, "TWD");
        historicalShippedOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(400m, "TWD"), 1);
        historicalShippedOrder.ChangeStatus(OrderStatus.Submitted);
        typeof(Order).GetProperty(nameof(Order.SubmittedAt))!
            .SetValue(historicalShippedOrder, new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero));
        historicalShippedOrder.MarkAsPaid();
        historicalShippedOrder.Ship();

        // F. Cancelled 訂單 (SubmittedAt = 2026-09-01 12:00:00Z)
        var cancelledOrder = Order.Create(customerId, "TWD");
        cancelledOrder.AddItem(new ProductId(Guid.NewGuid()), new Money(500m, "TWD"), 1);
        var addressCancelled = ShippingAddress.Create(
            $"Recipient {testRunPrefix} Cancelled",
            "+886912345674",
            "TW",
            "100",
            "Taipei",
            "Cancelled St",
            null).Value;
        var cancelledAtTime = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        cancelledOrder.Submit(addressCancelled, cancelledAtTime);
        cancelledOrder.Cancel();

        // 寫入真實 MySQL
        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.AddRange(
                pendingOrder,
                submittedOrder,
                paidOrder1,
                paidOrder2,
                historicalShippedOrder,
                cancelledOrder);
            await dbContext.SaveChangesAsync();
        }

        var client = CreateAdminClient();

        // =========================================================================
        // 1. Unfiltered list can retrieve multiple lifecycle states
        // =========================================================================
        var unfilteredResponse = await client.GetAsync("/api/v1/admin/orders?page=1&pageSize=50");
        unfilteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unfilteredResult = await unfilteredResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        unfilteredResult.Should().NotBeNull();
        unfilteredResult!.TotalCount.Should().BeGreaterThanOrEqualTo(6);

        var returnedIds = unfilteredResult.Items.Select(i => i.Id).ToList();
        returnedIds.Should().Contain(pendingOrder.Id.Value);
        returnedIds.Should().Contain(submittedOrder.Id.Value);
        returnedIds.Should().Contain(paidOrder1.Id.Value);
        returnedIds.Should().Contain(paidOrder2.Id.Value);
        returnedIds.Should().Contain(historicalShippedOrder.Id.Value);
        returnedIds.Should().Contain(cancelledOrder.Id.Value);

        // =========================================================================
        // 2. status=Shipped returns only Shipped
        // =========================================================================
        var shippedResponse = await client.GetAsync("/api/v1/admin/orders?status=Shipped");
        shippedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var shippedResult = await shippedResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        shippedResult.Should().NotBeNull();
        shippedResult!.Items.Should().NotBeEmpty();
        shippedResult.Items.Select(i => i.Status).Should().AllBeEquivalentTo("Shipped");
        shippedResult.Items.Select(i => i.Id).Should().Contain(historicalShippedOrder.Id.Value);

        // =========================================================================
        // 3. status=Cancelled returns only Cancelled
        // =========================================================================
        var cancelledResponse = await client.GetAsync("/api/v1/admin/orders?status=Cancelled");
        cancelledResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelledResult = await cancelledResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        cancelledResult.Should().NotBeNull();
        cancelledResult!.Items.Should().NotBeEmpty();
        cancelledResult.Items.Select(i => i.Status).Should().AllBeEquivalentTo("Cancelled");
        cancelledResult.Items.Select(i => i.Id).Should().Contain(cancelledOrder.Id.Value);

        // =========================================================================
        // 4. exact orderId filter returns only target order
        // =========================================================================
        var targetId = paidOrder1.Id.Value;
        var orderIdResponse = await client.GetAsync($"/api/v1/admin/orders?orderId={targetId}");
        orderIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orderIdResult = await orderIdResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        orderIdResult.Should().NotBeNull();
        orderIdResult!.TotalCount.Should().Be(1);
        orderIdResult.Items.Should().HaveCount(1);
        orderIdResult.Items[0].Id.Should().Be(targetId);
        orderIdResult.Items[0].Status.Should().Be("Paid");

        // =========================================================================
        // 5. combined orderId + mismatching status returns empty
        // =========================================================================
        var mismatchResponse = await client.GetAsync($"/api/v1/admin/orders?orderId={targetId}&status=Shipped");
        mismatchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var mismatchResult = await mismatchResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        mismatchResult.Should().NotBeNull();
        mismatchResult!.TotalCount.Should().Be(0);
        mismatchResult.Items.Should().BeEmpty();

        // =========================================================================
        // 6. pageSize bound is respected
        // =========================================================================
        var boundedResponse = await client.GetAsync("/api/v1/admin/orders?page=1&pageSize=2");
        boundedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var boundedResult = await boundedResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        boundedResult.Should().NotBeNull();
        boundedResult!.PageSize.Should().Be(2);
        boundedResult.Items.Should().HaveCount(2);

        // =========================================================================
        // 7. Deterministic ordering: SubmittedAt DESC then Id ASC
        // =========================================================================
        // When querying with status=Paid, we have paidOrder2 (10:30) and paidOrder1 (10:00)
        var paidOrderedResponse = await client.GetAsync("/api/v1/admin/orders?status=Paid");
        paidOrderedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paidOrderedResult = await paidOrderedResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        paidOrderedResult.Should().NotBeNull();
        var paidItems = paidOrderedResult!.Items.Where(i => i.Id == paidOrder1.Id.Value || i.Id == paidOrder2.Id.Value).ToList();
        paidItems.Should().HaveCount(2);
        // paidOrder2 (10:30) submitted after paidOrder1 (10:00), so DESC order puts paidOrder2 first
        paidItems[0].Id.Should().Be(paidOrder2.Id.Value);
        paidItems[1].Id.Should().Be(paidOrder1.Id.Value);

        // =========================================================================
        // 8. Admin detail retrieves a historical Shipped order
        // =========================================================================
        var detailShippedResponse = await client.GetAsync($"/api/v1/admin/orders/{historicalShippedOrder.Id.Value}");
        detailShippedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailShipped = await detailShippedResponse.Content.ReadFromJsonAsync<AdminOrderDetailResponse>();
        detailShipped.Should().NotBeNull();
        detailShipped!.Id.Should().Be(historicalShippedOrder.Id.Value);
        detailShipped.Status.Should().Be("Shipped");
        detailShipped.TotalAmount.Should().Be(400m);
        detailShipped.SubmittedAt.Should().Be(new DateTimeOffset(2026, 9, 1, 11, 0, 0, TimeSpan.Zero));
        detailShipped.Items.Should().HaveCount(1);

        // =========================================================================
        // 9. null ShippingAddress does not fail and returns safely
        // =========================================================================
        detailShipped.ShippingAddress.Should().BeNull();

        // Detail on Cancelled order with full ShippingAddress
        var detailCancelledResponse = await client.GetAsync($"/api/v1/admin/orders/{cancelledOrder.Id.Value}");
        detailCancelledResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailCancelled = await detailCancelledResponse.Content.ReadFromJsonAsync<AdminOrderDetailResponse>();
        detailCancelled.Should().NotBeNull();
        detailCancelled!.ShippingAddress.Should().NotBeNull();
        detailCancelled.ShippingAddress!.RecipientName.Should().Be($"Recipient {testRunPrefix} Cancelled");

        // =========================================================================
        // 10. TotalCount matches the applied filters
        // =========================================================================
        var statusPaidCountResponse = await client.GetAsync("/api/v1/admin/orders?status=Paid&page=1&pageSize=1");
        statusPaidCountResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusPaidCountResult = await statusPaidCountResponse.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        statusPaidCountResult.Should().NotBeNull();
        statusPaidCountResult!.Items.Should().HaveCount(1); // limited by pageSize
        statusPaidCountResult.TotalCount.Should().BeGreaterThanOrEqualTo(2); // filter-wide total, not current-page count

        // =========================================================================
        // 11. Pagination Acceptance: Non-overlapping slices & TotalCount integrity
        // =========================================================================
        var page1Response = await client.GetAsync("/api/v1/admin/orders?status=Paid&page=1&pageSize=1");
        var page2Response = await client.GetAsync("/api/v1/admin/orders?status=Paid&page=2&pageSize=1");

        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page1Result = await page1Response.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        var page2Result = await page2Response.Content.ReadFromJsonAsync<AdminOrderPageResponse>();

        page1Result.Should().NotBeNull();
        page2Result.Should().NotBeNull();

        page1Result!.Items.Should().HaveCount(1);
        page2Result!.Items.Should().HaveCount(1);

        // Non-overlapping items between page 1 and page 2
        page1Result.Items[0].Id.Should().NotBe(page2Result.Items[0].Id);

        // Both pages report identical TotalCount
        page1Result.TotalCount.Should().Be(page2Result.TotalCount);
    }
}
