using System.Net;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

[Collection("IntegrationTests")]
public class ECPayWebhookIntegrationTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;

    private const string MerchantId = "synthetic_merchant_123";
    private const string HashKey = "synthetic_hashkey_456";
    private const string HashIv = "synthetic_hashiv_789";

    public ECPayWebhookIntegrationTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.UseSetting("Payments:ECPay:MerchantId", MerchantId);
            builder.UseSetting("Payments:ECPay:HashKey", HashKey);
            builder.UseSetting("Payments:ECPay:HashIv", HashIv);
            builder.UseSetting("Payments:ECPay:ReturnUrl", "https://shop.example.com/api/v1/payments/webhooks/ecpay");

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

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    private static FormUrlEncodedContent CreateFormContent(
        Guid attemptGuid,
        string merchantId = MerchantId,
        string tradeNo = "ECPAY_TXN_888999",
        string tradeAmt = "1500",
        string rtnCode = "1",
        string simulatePaid = "0",
        string? customCheckMacValue = null,
        bool omitCheckMacValue = false,
        string? customField1 = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = merchantId,
            ["MerchantTradeNo"] = "TRADENO_" + Guid.NewGuid().ToString("N")[..10],
            ["RtnCode"] = rtnCode,
            ["RtnMsg"] = "Succeeded",
            ["TradeNo"] = tradeNo,
            ["TradeAmt"] = tradeAmt,
            ["PaymentDate"] = "2026/09/04 16:00:00",
            ["PaymentType"] = "Credit_CreditCard",
            ["TradeDate"] = "2026/09/04 15:59:00",
            ["SimulatePaid"] = simulatePaid,
            ["CustomField1"] = customField1 ?? attemptGuid.ToString("N"),
            ["CustomField2"] = Guid.NewGuid().ToString("N")
        };

        if (!omitCheckMacValue)
        {
            fields["CheckMacValue"] = customCheckMacValue ?? ECPayCheckMacValue.Generate(fields, HashKey, HashIv);
        }

        return new FormUrlEncodedContent(fields);
    }

    [Fact]
    public async Task Webhook_MissingCheckMacValue_ReturnsBadRequest_NoStateChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, omitCheckMacValue: true);

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);
        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public async Task Webhook_InvalidCheckMacValue_ReturnsBadRequest_NoStateChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, customCheckMacValue: "TAMPERED_CHECK_MAC_VALUE_123");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);
        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public async Task Webhook_MerchantIdMismatch_ReturnsBadRequest_NoStateChange()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, merchantId: "foreign_merchant_999");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);
    }

    [Fact]
    public async Task Webhook_InvalidCustomField1Guid_ReturnsBadRequest_NoStateChange()
    {
        var client = _factory.CreateClient();
        var content = CreateFormContent(Guid.NewGuid(), customField1: "not-a-valid-guid");

        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_ValidGenuineSuccess_TransitionsOrderToPaid_AndAttemptToSucceeded_ReturnsExactOk()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1200m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_GENUINE_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "1200", rtnCode: "1", simulatePaid: "0");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("1|OK");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        dbAttempt.ProviderTransactionId.Should().Be(tradeNo);

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Paid);

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(1);
    }

    [Fact]
    public async Task Webhook_DuplicateDelivery_IsIdempotent_ReturnsExactOk()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(800m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_DUP_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();

        // 第一次推送
        var content1 = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "800", rtnCode: "1", simulatePaid: "0");
        var resp1 = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content1);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp1.Content.ReadAsStringAsync()).Should().Be("1|OK");

        // 第二次推送（完全相同）
        var content2 = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "800", rtnCode: "1", simulatePaid: "0");
        var resp2 = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content2);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp2.Content.ReadAsStringAsync()).Should().Be("1|OK");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Paid);

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(1);
    }

    [Fact]
    public async Task Webhook_SimulatePaidEquals1_DoesNotMarkOrderPaid_ReturnsExactOk()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1500m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_SIMULATE_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "1500", rtnCode: "1", simulatePaid: "1");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("1|OK");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted);

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(0);
    }

    [Fact]
    public async Task Webhook_NonSuccessRtnCode_DoesNotMarkOrderPaid_ReturnsExactOk()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1500m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_NON_SUCCESS_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        // RtnCode != "1", e.g. "10100058"
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "1500", rtnCode: "10100058", simulatePaid: "0");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("1|OK");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending); // 保守策略，不盲目標記 Failed

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted);
    }

    [Fact]
    public async Task Webhook_ProviderMismatch_DoesNotMarkOrderPaid()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(2000m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        // Attempt 的 Provider 是 Stripe，不是 ECPay
        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "Stripe", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_MISMATCH_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "2000", rtnCode: "1", simulatePaid: "0");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted); // 不應變為 Paid

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(0);
    }

    [Fact]
    public async Task Webhook_AmountMismatch_DoesNotMarkOrderPaid()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(3000m, "TWD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_AMT_MISMATCH_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        // Attempt 金額是 3000，但回傳金額是 1000
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "1000", rtnCode: "1", simulatePaid: "0");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted); // 金額不符，不應變為 Paid

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(0);
    }

    [Fact]
    public async Task Webhook_CurrencyMismatch_DoesNotMarkOrderPaid()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        // Attempt 幣別是 USD
        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var tradeNo = "TRADE_CURR_MISMATCH_" + Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        // ECPay 回傳 TradeAmt 100（正規化為 TWD）
        var content = CreateFormContent(attempt.Id.Value, tradeNo: tradeNo, tradeAmt: "100", rtnCode: "1", simulatePaid: "0");

        // Act
        var response = await client.PostAsync("/api/v1/payments/webhooks/ecpay", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var dbAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        dbAttempt.Status.Should().Be(PaymentAttemptStatus.Pending);

        var dbOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Submitted); // 幣別不符，不應變為 Paid

        var receiptCount = await verifyDb.PaymentWebhookReceipts.CountAsync(r => r.Provider == "ECPay" && r.ProviderEventId == tradeNo);
        receiptCount.Should().Be(0);
    }
}
