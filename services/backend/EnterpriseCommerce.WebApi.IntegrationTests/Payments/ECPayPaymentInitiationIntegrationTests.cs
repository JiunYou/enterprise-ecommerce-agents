using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Payments;
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
public class ECPayPaymentInitiationIntegrationTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;

    private const string MerchantId = "synthetic_merchant_123";
    private const string HashKey = "synthetic_hashkey_456";
    private const string HashIv = "synthetic_hashiv_789";
    private const string ReturnUrl = "https://shop.example.com/api/v1/payments/webhooks/ecpay";
    private const string ClientBackUrlBase = "http://localhost:3001";
    private const string ActionUrl = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

    public ECPayPaymentInitiationIntegrationTests(MySqlFixture mySqlFixture)
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
            builder.UseSetting("Payments:ECPay:ReturnUrl", ReturnUrl);
            builder.UseSetting("Payments:ECPay:ClientBackUrlBase", ClientBackUrlBase);
            builder.UseSetting("Payments:ECPay:ActionUrl", ActionUrl);

            builder.ConfigureTestServices(services =>
            {
                // 不覆寫 IPaymentProvider，測試生產 DI 容器中註冊的 ECPayPaymentProvider
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

    [Fact]
    public void ProductionContainer_ResolvesECPayPaymentProvider_AsSingleActiveProvider()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        provider.Should().NotBeNull();
        provider.Should().BeOfType<ECPayPaymentProvider>();
        provider.ProviderName.Should().Be("ECPay");
    }

    [Fact]
    public async Task InitiatePayment_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new InitiatePaymentRequest(Guid.NewGuid(), Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InitiatePayment_NonOwner_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var order = Order.Create(ownerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", attackerId.ToString());

        var request = new InitiatePaymentRequest(order.Id.Value, Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InitiatePayment_NonSubmittedOrder_ReturnsBadRequest()
    {
        var ownerId = Guid.NewGuid();

        // 處於 Draft 狀態而非 Submitted
        var order = Order.Create(ownerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var request = new InitiatePaymentRequest(order.Id.Value, Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitiatePayment_OwnerSubmittedOrder_ReturnsECPayPostLaunchResponse()
    {
        var ownerId = Guid.NewGuid();

        var order = Order.Create(ownerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(2500m, "TWD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var idempotencyKey = Guid.NewGuid();
        var request = new InitiatePaymentRequest(order.Id.Value, idempotencyKey);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var launchData = await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>();

        launchData.Should().NotBeNull();
        launchData!.ProviderTransactionId.Should().BeNull();
        launchData.Method.Should().Be(PaymentLaunchMethod.Post);
        launchData.ActionUrl.Should().Be(ActionUrl);
        launchData.FormFields.Should().NotBeNull();

        var fields = launchData.FormFields!;
        fields.Should().ContainKey("MerchantID").WhoseValue.Should().Be(MerchantId);
        fields.Should().ContainKey("MerchantTradeNo");
        fields.Should().ContainKey("MerchantTradeDate");
        fields.Should().ContainKey("PaymentType").WhoseValue.Should().Be("aio");
        fields.Should().ContainKey("TotalAmount").WhoseValue.Should().Be("2500");
        fields.Should().ContainKey("ReturnURL").WhoseValue.Should().Be(ReturnUrl);
        fields.Should().ContainKey("ChoosePayment").WhoseValue.Should().Be("Credit");
        fields.Should().ContainKey("ClientBackURL").WhoseValue.Should().Be($"{ClientBackUrlBase}/orders/{order.Id.Value}?payment=returned");
        fields.Should().NotContainKey("OrderResultURL");
        fields.Should().ContainKey("CheckMacValue");
        fields["CheckMacValue"].Should().NotBeNullOrWhiteSpace();
        fields.Should().ContainKey("CustomField1");
        fields.Should().ContainKey("CustomField2");

        // 驗證真實 MySQL 寫入
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var attempt = await verifyDb.PaymentAttempts.FirstOrDefaultAsync(pa => pa.OrderId == order.Id);

            attempt.Should().NotBeNull();
            attempt!.Status.Should().Be(PaymentAttemptStatus.Pending);
            attempt.Provider.Should().Be("ECPay");
            attempt.Amount.Amount.Should().Be(2500m);
            attempt.Amount.Currency.Should().Be("TWD");
            attempt.IdempotencyKey.Should().Be(idempotencyKey);

            fields["CustomField1"].Should().Be(attempt.Id.Value.ToString("N"));
            fields["CustomField2"].Should().Be(order.Id.Value.ToString("N"));
        }
    }

    [Fact]
    public async Task InitiatePayment_ECPay_SameIdempotencyKey_ReusesPaymentAttemptAndSameMerchantTradeNo()
    {
        var ownerId = Guid.NewGuid();
        var order = Order.Create(ownerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var idempotencyKey = Guid.NewGuid();
        var request = new InitiatePaymentRequest(order.Id.Value, idempotencyKey);

        // Act 1
        var response1 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var launch1 = await response1.Content.ReadFromJsonAsync<InitiatePaymentResponse>();

        // Act 2: Same idempotency key
        var response2 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var launch2 = await response2.Content.ReadFromJsonAsync<InitiatePaymentResponse>();

        // Assert
        launch1!.FormFields!["MerchantTradeNo"].Should().Be(launch2!.FormFields!["MerchantTradeNo"]);
        launch1.FormFields!["CheckMacValue"].Should().Be(launch2.FormFields!["CheckMacValue"]);
        launch1.FormFields!["CustomField1"].Should().Be(launch2.FormFields!["CustomField1"]);

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var attempts = await verifyDb.PaymentAttempts.Where(pa => pa.OrderId == order.Id).ToListAsync();
            attempts.Should().ContainSingle();
            attempts[0].Status.Should().Be(PaymentAttemptStatus.Pending);
            attempts[0].IdempotencyKey.Should().Be(idempotencyKey);
        }
    }

    [Fact]
    public async Task InitiatePayment_ECPay_DifferentIdempotencyKey_CreatesNewPaymentAttemptAndDistinctMerchantTradeNo()
    {
        var ownerId = Guid.NewGuid();
        var order = Order.Create(ownerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(1000m, "TWD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();

        // Act 1: Initial initiate Key A
        var responseA = await client.PostAsJsonAsync("/api/v1/payments/initiate", new InitiatePaymentRequest(order.Id.Value, keyA));
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var launchA = await responseA.Content.ReadFromJsonAsync<InitiatePaymentResponse>();

        // Act 2: Customer returns after abandon and initiates Key B
        var responseB = await client.PostAsJsonAsync("/api/v1/payments/initiate", new InitiatePaymentRequest(order.Id.Value, keyB));
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var launchB = await responseB.Content.ReadFromJsonAsync<InitiatePaymentResponse>();

        // Assert: Distinct MerchantTradeNo derived from distinct PaymentAttemptIds
        launchA!.FormFields!["MerchantTradeNo"].Should().NotBe(launchB!.FormFields!["MerchantTradeNo"]);
        launchA.FormFields!["CustomField1"].Should().NotBe(launchB.FormFields!["CustomField1"]);

        // Verify MySQL state: 2 attempts, both Pending, distinct IDs and keys
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var attempts = await verifyDb.PaymentAttempts.Where(pa => pa.OrderId == order.Id).OrderBy(pa => pa.CreatedAt).ToListAsync();
            attempts.Should().HaveCount(2);

            attempts[0].Status.Should().Be(PaymentAttemptStatus.Pending);
            attempts[0].IdempotencyKey.Should().Be(keyA);

            attempts[1].Status.Should().Be(PaymentAttemptStatus.Pending);
            attempts[1].IdempotencyKey.Should().Be(keyB);

            attempts[0].Id.Should().NotBe(attempts[1].Id);
        }
    }

    private static ShippingAddress CreateTestShippingAddress()
    {
        return ShippingAddress.Create("Test Customer", "0912345678", "TW", "100", "Taipei", "123 Main St").Value;
    }
}
