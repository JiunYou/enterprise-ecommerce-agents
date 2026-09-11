using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
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
public class CustomerRefundStatusVisibilityAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CustomerRefundStatusVisibilityAcceptanceTests(MySqlFixture mySqlFixture)
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

    private static ShippingAddress CreateTestAddress()
    {
        return ShippingAddress.Create(
            "Test Customer",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Main St 1").Value;
    }

    [Fact]
    public async Task GetOrderById_WhenRefundRequiredWithoutPaymentRefund_ReturnsRequiredStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(150m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            "AUTH_REF_1");
        attempt.MarkAsRefundRequired("TX_1", DateTimeOffset.UtcNow.AddMinutes(-10), "AUTH_REF_1");

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        node.Should().NotBeNull();
        var refunds = node!["refunds"] as JsonArray;
        refunds.Should().NotBeNull();
        refunds!.Count.Should().Be(1);

        var first = refunds[0]!;
        first["amount"]!.GetValue<decimal>().Should().Be(150m);
        first["currency"]!.GetValue<string>().Should().Be("TWD");
        first["status"]!.GetValue<string>().Should().Be("Required");
        first["requestedAt"].Should().BeNull();
        first["completedAt"].Should().BeNull();
    }

    [Fact]
    public async Task GetOrderById_WhenPendingRefund_ReturnsProcessingStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(200m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            "AUTH_REF_2");
        attempt.MarkAsRefundRequired("TX_2", DateTimeOffset.UtcNow.AddMinutes(-15), "AUTH_REF_2");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 14, 0, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer refund request", "issuer-admin", "admin-sub", requestedAt).Value;

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            db.PaymentRefunds.Add(refund);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        var refunds = node!["refunds"] as JsonArray;
        refunds.Should().NotBeNull();
        refunds!.Count.Should().Be(1);

        var first = refunds[0]!;
        first["amount"]!.GetValue<decimal>().Should().Be(200m);
        first["currency"]!.GetValue<string>().Should().Be("TWD");
        first["status"]!.GetValue<string>().Should().Be("Processing");
        first["requestedAt"].Should().NotBeNull();
        first["completedAt"].Should().BeNull();
    }

    [Fact]
    public async Task GetOrderById_WhenSucceededRefund_ReturnsSucceededStatusWithCompletedAt()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(300m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            "AUTH_REF_3");
        attempt.MarkAsRefundRequired("TX_3", DateTimeOffset.UtcNow.AddMinutes(-15), "AUTH_REF_3");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 14, 0, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 9, 11, 14, 10, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer refund request", "issuer-admin", "admin-sub", requestedAt).Value;
        refund.MarkAsSucceeded(completedAt);

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            db.PaymentRefunds.Add(refund);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        var refunds = node!["refunds"] as JsonArray;
        refunds.Should().NotBeNull();
        refunds!.Count.Should().Be(1);

        var first = refunds[0]!;
        first["amount"]!.GetValue<decimal>().Should().Be(300m);
        first["currency"]!.GetValue<string>().Should().Be("TWD");
        first["status"]!.GetValue<string>().Should().Be("Succeeded");
        first["requestedAt"].Should().NotBeNull();
        first["completedAt"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrderById_WhenFailedOrUnresolvedRefund_ReturnsNeedsReviewStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attemptFailed = PaymentAttempt.Create(
            order.Id,
            new Money(100m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-25),
            "AUTH_FAIL");
        attemptFailed.MarkAsRefundRequired("TX_FAIL", DateTimeOffset.UtcNow.AddMinutes(-20), "AUTH_FAIL");

        var refundFailed = PaymentRefund.Create(attemptFailed.Id, "Reason", "iss", "sub", DateTimeOffset.UtcNow.AddMinutes(-18)).Value;
        refundFailed.MarkAsFailed(DateTimeOffset.UtcNow.AddMinutes(-15));

        var attemptUnresolved = PaymentAttempt.Create(
            order.Id,
            new Money(200m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-10),
            "AUTH_UNRES");
        attemptUnresolved.MarkAsRefundRequired("TX_UNRES", DateTimeOffset.UtcNow.AddMinutes(-8), "AUTH_UNRES");

        var refundUnresolved = PaymentRefund.Create(attemptUnresolved.Id, "Reason", "iss", "sub", DateTimeOffset.UtcNow.AddMinutes(-5)).Value;
        refundUnresolved.MarkAsUnresolved();

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.AddRange(attemptFailed, attemptUnresolved);
            db.PaymentRefunds.AddRange(refundFailed, refundUnresolved);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        var refunds = node!["refunds"] as JsonArray;
        refunds.Should().NotBeNull();
        refunds!.Count.Should().Be(2);

        foreach (var r in refunds!)
        {
            r!["status"]!.GetValue<string>().Should().Be("NeedsReview");
        }
    }

    [Fact]
    public async Task GetOrderById_WhenCrossCustomer_ReturnsNotFoundAndPreservesBoundary()
    {
        // Arrange
        var ownerCustomerId = Guid.NewGuid();
        var attackerCustomerId = Guid.NewGuid();
        var attackerClient = CreateCustomerClient(attackerCustomerId);

        var order = Order.Create(ownerCustomerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(500m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            "AUTH_SECRET");
        attempt.MarkAsRefundRequired("TX_SECRET", DateTimeOffset.UtcNow.AddMinutes(-15), "AUTH_SECRET");

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await attackerClient.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrderById_WhenRefundExists_CustomerPayloadDoesNotExposeSensitiveFields()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));
        order.Cancel();

        var attempt = PaymentAttempt.Create(
            order.Id,
            new Money(777m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-20),
            "AUTH_SECRET_VAL");
        attempt.MarkAsRefundRequired("TX_PROVIDER_ID", DateTimeOffset.UtcNow.AddMinutes(-15), "AUTH_SECRET_VAL");

        var refund = PaymentRefund.Create(attempt.Id, "Sensitive internal refund reason", "admin-issuer-sensitive", "admin-subject-sensitive", DateTimeOffset.UtcNow.AddMinutes(-10)).Value;
        refund.MarkAsSucceeded(DateTimeOffset.UtcNow.AddMinutes(-5));

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            db.PaymentRefunds.Add(refund);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rawJson = await response.Content.ReadAsStringAsync();

        // 機械式敏感欄位檢查
        var forbiddenKeywords = new[]
        {
            "paymentAttemptId",
            "provider",
            "providerTransactionId",
            "providerAuthorizationReference",
            "idempotencyKey",
            "reason",
            "actorIssuer",
            "actorSubject",
            "capability",
            "AUTH_SECRET_VAL",
            "TX_PROVIDER_ID",
            "Sensitive internal refund reason",
            "admin-issuer-sensitive",
            "admin-subject-sensitive"
        };

        foreach (var keyword in forbiddenKeywords)
        {
            rawJson.Should().NotContainEquivalentOf(
                $"\"{keyword}\"",
                because: $"Customer refund response must never leak internal or sensitive payment field '{keyword}'");
        }

        // 確認退款物件只具備預期的公開屬性：amount, currency, status, requestedAt, completedAt
        using var doc = JsonDocument.Parse(rawJson);
        var refundsElement = doc.RootElement.GetProperty("refunds");
        refundsElement.GetArrayLength().Should().Be(1);

        var refundObj = refundsElement[0];
        var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "amount",
            "currency",
            "status",
            "requestedAt",
            "completedAt"
        };

        foreach (var prop in refundObj.EnumerateObject())
        {
            allowedProperties.Should().Contain(prop.Name, because: $"Property '{prop.Name}' is not allowed in CustomerRefundStatusResponse");
        }
    }

    [Fact]
    public async Task GetOrderById_WhenNoRefundRequired_RefundsIsEmptyArray()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);
        var order = Order.Create(customerId, "TWD");
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow.AddMinutes(-30));

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(json);
        var refunds = node!["refunds"] as JsonArray;
        refunds.Should().NotBeNull();
        refunds!.Count.Should().Be(0);
    }
}
