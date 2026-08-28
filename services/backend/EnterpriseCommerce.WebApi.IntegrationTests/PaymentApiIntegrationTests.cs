using EnterpriseCommerce.Domain.Orders;
using Microsoft.Extensions.Configuration;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.WebApi.Contracts.Payments;
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
public class PaymentApiIntegrationTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;

    public PaymentApiIntegrationTests(MySqlFixture mySqlFixture)
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
            builder.UseSetting("EnableDummyWebhook", "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(EnterpriseCommerce.WebApi.IntegrationTests.Controllers.TestDummyWebhookController).Assembly);
                services.AddScoped<EnterpriseCommerce.Application.Payments.IPaymentProvider, EnterpriseCommerce.WebApi.IntegrationTests.Payments.DummyPaymentProvider>();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, options => { });
            });
        });
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InitiatePayment_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory!.CreateClient();
        var request = new InitiatePaymentRequest(Guid.NewGuid(), Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InitiatePayment_Owner_ReturnsOk()
    {
        var ownerId = Guid.NewGuid();
        var order = Order.Create(ownerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var request = new InitiatePaymentRequest(order.Id.Value, Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Because: " + content);
    }

    [Fact]
    public async Task InitiatePayment_NonOwner_ReturnsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var nonOwnerId = Guid.NewGuid();
        var order = Order.Create(ownerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", nonOwnerId.ToString());

        var request = new InitiatePaymentRequest(order.Id.Value, Guid.NewGuid());
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "Because: " + content);
    }
    [Fact]
    public async Task InitiatePayment_SameIdempotencyKey_SameOrder_ReturnsSameProviderTxId()
    {
        var ownerId = Guid.NewGuid();
        var order = Order.Create(ownerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var idempotencyKey = Guid.NewGuid();
        var request = new InitiatePaymentRequest(order.Id.Value, idempotencyKey);
        
        // Act 1
        var response1 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);
        var content1 = await response1.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        
        // Act 2 (Simulate retry after crash)
        var response2 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);
        var content2 = await response2.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        
        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        content2!.ProviderTransactionId.Should().Be(content1!.ProviderTransactionId, "Because the retry with same idempotency key should reuse the same PaymentAttemptId, and the provider is idempotent based on PaymentAttemptId");
        
        // Verify only 1 PaymentAttempt was created in DB
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var attemptCount = await verifyDb.PaymentAttempts.CountAsync(pa => pa.OrderId == order.Id);
        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task InitiatePayment_SameIdempotencyKey_DifferentOrder_ReturnsDifferentProviderTxId()
    {
        var ownerId = Guid.NewGuid();
        var order1 = Order.Create(ownerId, "USD");
        order1.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order1.Submit(DateTimeOffset.UtcNow);
        
        var order2 = Order.Create(ownerId, "USD");
        order2.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(200m, "USD"), 1);
        order2.Submit(DateTimeOffset.UtcNow);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        db.Orders.AddRange(order1, order2);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", ownerId.ToString());

        var idempotencyKey = Guid.NewGuid(); // SAME key
        var request1 = new InitiatePaymentRequest(order1.Id.Value, idempotencyKey);
        var request2 = new InitiatePaymentRequest(order2.Id.Value, idempotencyKey);
        
        // Act
        var response1 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request1);
        var content1 = await response1.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        
        var response2 = await client.PostAsJsonAsync("/api/v1/payments/initiate", request2);
        var content2 = await response2.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        
        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        content2!.ProviderTransactionId.Should().NotBe(content1!.ProviderTransactionId, "Because they are different orders, so they generate different PaymentAttempts despite having the same client idempotency key");
    }

    [Fact]
    public async Task ProcessDummyWebhook_WithoutTestApplicationPart_Returns404NotFound()
    {
        // Arrange
        // Create a standard WebApplicationFactory without adding the TestDummyWebhookController part.
        // Even if EnableDummyWebhook = true, the endpoint should not exist because it's not in the production assembly.
        using var productionFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.UseSetting("EnableDummyWebhook", "true");
            // NOT calling AddApplicationPart(typeof(TestDummyWebhookController).Assembly)
        });

        var client = productionFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dummy-Signature", "dummy-secret-123");

        var payload = new EnterpriseCommerce.WebApi.IntegrationTests.Controllers.DummyWebhookPayload(
            Guid.NewGuid(), "evt_1", "tx_1", 100m, "USD", true);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/webhook/dummy", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "Because the dummy webhook controller does not exist in the production assembly.");
    }
}
