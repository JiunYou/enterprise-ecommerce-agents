using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Cart;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using EnterpriseCommerce.WebApi.Contracts.Payments;
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
public class MoneyChainAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    private const string MerchantId = "synthetic_merchant_123";
    private const string HashKey = "synthetic_hashkey_456";
    private const string HashIv = "synthetic_hashiv_789";
    private const string ReturnUrl = "https://shop.example.com/api/v1/payments/webhooks/ecpay";
    private const string ClientBackUrlBase = "http://localhost:3001";
    private const string ActionUrl = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

    public MoneyChainAcceptanceTests(MySqlFixture mySqlFixture)
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
            builder.UseSetting("Payments:ECPay:MerchantId", MerchantId);
            builder.UseSetting("Payments:ECPay:HashKey", HashKey);
            builder.UseSetting("Payments:ECPay:HashIv", HashIv);
            builder.UseSetting("Payments:ECPay:ReturnUrl", ReturnUrl);
            builder.UseSetting("Payments:ECPay:ClientBackUrlBase", ClientBackUrlBase);
            builder.UseSetting("Payments:ECPay:ActionUrl", ActionUrl);

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, _ => { });
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
    public async Task CompleteMoneyChain_RealMySql_Subtotal1000_Discount100_OrderTotal900_PaymentAttemptAmount900()
    {
        // 1. Arrange: Product (1000 TWD) + Inventory (10) + Coupon (100 TWD discount)
        var customerId = Guid.NewGuid();
        var product = Product.Create("Chain Test Product", $"SKU-{Guid.NewGuid():N}", 1000m, "TWD").Value;
        var productId = product.Id;
        var inventory = InventoryItem.Create(new ProductReference(productId));
        inventory.IncreaseStock(new StockQuantity(10));

        var couponCode = $"MC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var coupon = Coupon.Create(
            couponCode,
            100m,
            "TWD",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow).Value;

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

        // 2. Customer adds item to cart (Subtotal = 1000 TWD)
        var addItemResponse = await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(productId, 1));
        addItemResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify cart before coupon
        var initialCart = await client.GetFromJsonAsync<CartResponse>("/api/v1/cart");
        initialCart.Should().NotBeNull();
        initialCart!.SubtotalAmount.Should().Be(1000m);
        initialCart.DiscountAmount.Should().Be(0m);
        initialCart.TotalAmount.Should().Be(1000m);
        initialCart.AppliedCouponCode.Should().BeNull();

        // 3. Customer applies Coupon (100 TWD)
        var applyResponse = await client.PutAsJsonAsync("/api/v1/cart/coupon", new ApplyCouponRequest(couponCode));
        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var discountedCart = await applyResponse.Content.ReadFromJsonAsync<CartResponse>();
        discountedCart.Should().NotBeNull();
        discountedCart!.SubtotalAmount.Should().Be(1000m);
        discountedCart.DiscountAmount.Should().Be(100m);
        discountedCart.TotalAmount.Should().Be(900m);
        discountedCart.AppliedCouponCode.Should().Be(couponCode);

        // 4. Submit Order
        var orderId = discountedCart.Id;
        orderId.Should().NotBeNull();
        var submitRequest = new SubmitOrderRequest(new ShippingAddressRequest(
            "Recipient", "0912345678", "TW", "100", "Taipei", "Main St 1", "3F"));
        var submitResponse = await client.PutAsJsonAsync($"/api/v1/orders/{orderId}/submit", submitRequest);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var orderGetResp = await client.GetAsync($"/api/v1/orders/{orderId}");
        orderGetResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var submittedOrder = await orderGetResp.Content.ReadFromJsonAsync<OrderResponse>();
        submittedOrder.Should().NotBeNull();
        submittedOrder!.SubtotalAmount.Should().Be(1000m);
        submittedOrder.DiscountAmount.Should().Be(100m);
        submittedOrder.TotalAmount.Should().Be(900m);
        submittedOrder.AppliedCouponCode.Should().Be(couponCode);

        // Verify database persistence of Order snapshot and amounts
        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var persistedOrder = await db.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == new OrderId(submittedOrder.Id));
            persistedOrder.SubtotalAmount.Amount.Should().Be(1000m);
            persistedOrder.DiscountAmount.Amount.Should().Be(100m);
            persistedOrder.TotalAmount.Amount.Should().Be(900m);
            persistedOrder.Currency.Should().Be("TWD");
            persistedOrder.AppliedCouponCode.Should().Be(couponCode);
            persistedOrder.AppliedCouponDiscountAmount.Should().Be(100m);
            persistedOrder.AppliedCouponExpiresAt.Should().BeCloseTo(coupon.ExpiresAt, TimeSpan.FromSeconds(1));
        }

        // 5. Initiate Payment
        var idempotencyKey = Guid.NewGuid();
        var initiatePaymentRequest = new InitiatePaymentRequest(submittedOrder.Id, idempotencyKey);
        var paymentResponse = await client.PostAsJsonAsync("/api/v1/payments/initiate", initiatePaymentRequest);
        paymentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var launchData = await paymentResponse.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        launchData.Should().NotBeNull();
        launchData!.FormFields.Should().ContainKey("TotalAmount").WhoseValue.Should().Be("900");

        // 6. Verify PaymentAttempt in Database matches discounted Order.TotalAmount exactly
        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var paymentAttempt = await db.PaymentAttempts
                .SingleAsync(pa => pa.OrderId == new OrderId(submittedOrder.Id));

            paymentAttempt.Amount.Amount.Should().Be(900m);
            paymentAttempt.Amount.Currency.Should().Be("TWD");
            paymentAttempt.OrderId.Value.Should().Be(submittedOrder.Id);
        }
    }
}
