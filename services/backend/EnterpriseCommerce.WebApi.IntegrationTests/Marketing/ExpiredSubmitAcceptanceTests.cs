using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Cart;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

[Collection("IntegrationTests")]
public class ExpiredSubmitAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;
    private readonly TestSettableTimeProvider _testTimeProvider;

    public ExpiredSubmitAcceptanceTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
        _testTimeProvider = new TestSettableTimeProvider(DateTimeOffset.UtcNow);
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
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, _ => { });

                // 替換 TimeProvider 為可控時間
                services.AddSingleton<TimeProvider>(_testTimeProvider);
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

    [Fact]
    public async Task SubmitOrder_WithExpiredAppliedCoupon_Fails_OrderRemainsPending_InventoryUnchanged_NoPayment()
    {
        // 1. Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Expired Test Product", $"SKU-{Guid.NewGuid():N}", 1000m, "TWD").Value;
        var productId = product.Id;
        var inventory = InventoryItem.Create(new ProductReference(productId));
        inventory.IncreaseStock(new StockQuantity(50));

        var baseTime = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        _testTimeProvider.SetUtcNow(baseTime);

        // 優惠券有效時間: baseTime - 1h 到 baseTime + 1h
        var couponCode = $"EXP-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var couponExpiresAt = baseTime.AddHours(1);
        var coupon = Coupon.Create(
            couponCode,
            200m,
            "TWD",
            baseTime.AddHours(-1),
            couponExpiresAt,
            baseTime).Value;

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Products.Add(product);
            db.InventoryItems.Add(inventory);
            db.Coupons.Add(coupon);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // 加入購物車
        var addItemResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(productId, 1));
        addItemResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 套用優惠券 (此時有效)
        var applyResponse = await client.PutAsJsonAsync("/api/v1/cart/coupon", new ApplyCouponRequest(couponCode));
        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. 推進伺服器時間至優惠券過期之後 (couponExpiresAt + 10 分鐘)
        _testTimeProvider.SetUtcNow(couponExpiresAt.AddMinutes(10));

        // 3. 執行 SubmitOrder (必須失敗，不允許逾期提交、不允許靜默移除、不允許原價提交)
        var cartResp = await client.GetFromJsonAsync<EnterpriseCommerce.Application.Orders.Queries.GetCart.CartResponse>("/api/v1/cart");
        var orderId = cartResp!.Id;
        orderId.Should().NotBeNull();

        var submitRequest = new SubmitOrderRequest(new ShippingAddressRequest(
            "Recipient", "0912345678", "TW", "100", "Taipei", "Main St 1", "3F"));
        var submitResponse = await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/submit", submitRequest);

        // 驗證提交失敗 (400 Bad Request)
        submitResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await submitResponse.Content.ReadAsStringAsync();
        content.Should().Contain(OrderErrors.AppliedCouponExpired.Message);

        // 4. 驗證資料庫狀態：
        // (a) Order 依然維持 Pending 狀態
        // (b) 優惠券快照依然存在 (未被靜默移除)
        // (c) 庫存依然為初始值 50 (未被預扣)
        // (d) 無任何 PaymentAttempt 產生
        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var pendingOrder = await db.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.CustomerId == customerId);

            pendingOrder.Status.Should().Be(OrderStatus.Pending);
            pendingOrder.AppliedCouponCode.Should().Be(couponCode);
            pendingOrder.AppliedCouponDiscountAmount.Should().Be(200m);
            pendingOrder.DiscountAmount.Amount.Should().Be(200m);
            pendingOrder.TotalAmount.Amount.Should().Be(800m); // 依然維持折抵金額，未靜默改回 1000

            var dbInventory = await db.InventoryItems
                .SingleAsync(i => i.ProductReference == new ProductReference(productId));
            dbInventory.AvailableQuantity.Value.Should().Be(50); // 庫存未變動

            var paymentCount = await db.PaymentAttempts
                .CountAsync(pa => pa.OrderId == pendingOrder.Id);
            paymentCount.Should().Be(0); // 無付款
        }
    }

    private sealed class TestSettableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
