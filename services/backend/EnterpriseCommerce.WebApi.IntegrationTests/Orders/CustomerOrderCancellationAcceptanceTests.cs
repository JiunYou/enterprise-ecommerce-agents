using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
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
public class CustomerOrderCancellationAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CustomerOrderCancellationAcceptanceTests(MySqlFixture mySqlFixture)
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
            "Alice",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Main St 1").Value;
    }

    [Fact]
    public async Task CancelOrder_WhenAnonymous_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = CreateCustomerClient(anonymous: true);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOrder_WhenAuthenticatedWithoutCustomerIdClaim_ShouldReturn403Forbidden()
    {
        // Arrange
        var client = CreateCustomerClient(customerId: null, anonymous: false);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelOrder_WhenOtherCustomerOrder_ShouldReturn404NotFoundAndKeepStateUnchanged()
    {
        // Arrange
        var customerOwner = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        var order = Order.Create(customerOwner, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(200m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(customerId: otherCustomer);

        // Act
        var response = await client.PutAsync($"/api/v1/orders/{order.Id.Value}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloaded = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            reloaded.Status.Should().Be(OrderStatus.Submitted);
        }
    }

    [Fact]
    public async Task CancelOrder_WhenOwnSubmittedOrder_ShouldCancelAndPersistOutboxEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(300m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = CreateCustomerClient(customerId: customerId);

        // Act
        var response = await client.PutAsync($"/api/v1/orders/{order.Id.Value}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloaded = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            reloaded.Status.Should().Be(OrderStatus.Cancelled);

            var outboxMessages = await verifyDb.OutboxMessages
                .Where(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains(order.Id.Value.ToString()))
                .ToListAsync();

            outboxMessages.Should().Contain(m => m.Content.Contains("\"NewStatus\":4"));
        }
    }

    [Fact]
    public async Task CancelOrder_WhenOwnPaidOrder_ShouldReturn400BadRequestAndRemainPaidWithoutOutboxCancelEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(500m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);
        order.MarkAsPaid();

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        int outboxCountBefore;
        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            outboxCountBefore = await verifyDb.OutboxMessages
                .CountAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains(order.Id.Value.ToString()));
        }

        var client = CreateCustomerClient(customerId: customerId);

        // Act
        var response = await client.PutAsync($"/api/v1/orders/{order.Id.Value}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloaded = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            reloaded.Status.Should().Be(OrderStatus.Paid);

            var outboxCountAfter = await verifyDb.OutboxMessages
                .CountAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains(order.Id.Value.ToString()));

            outboxCountAfter.Should().Be(outboxCountBefore, "No cancellation outbox message should be persisted for rejected Paid order cancellation");
        }
    }

    [Fact]
    public async Task CUSTOMER_CANCEL_PAYMENT_RACE_CANCEL_WINS_LATE_WEBHOOK_BECOMES_REFUND_REQUIRED()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);

        var attempt = EnterpriseCommerce.Domain.Payments.PaymentAttempt.Create(
            order.Id,
            order.TotalAmount,
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Act 1: Customer Submitted cancellation commits first via HTTP
        var client = CreateCustomerClient(customerId: customerId);
        var cancelResponse = await client.PutAsync($"/api/v1/orders/{order.Id.Value}/cancel", null);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: Process payment webhook success arrives later
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var webhookCommand = new EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook.ProcessPaymentWebhookCommand(
                attempt.Id.Value,
                "ECPay",
                "evt-" + Guid.NewGuid().ToString("N"),
                "tx-" + Guid.NewGuid().ToString("N"),
                100m,
                "TWD",
                true);

            var webhookResult = await sender.Send(webhookCommand);
            webhookResult.IsSuccess.Should().BeTrue();
        }

        // Assert: Order remains Cancelled and attempt is RefundRequired
        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled, "Order must remain Cancelled and NOT become Paid");

            var finalAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
            finalAttempt.Status.Should().Be(EnterpriseCommerce.Domain.Payments.PaymentAttemptStatus.RefundRequired, "Late payment on cancelled order must be marked RefundRequired");
        }
    }

    [Fact]
    public async Task CUSTOMER_CANCEL_PAYMENT_RACE_PAYMENT_WINS_CANCEL_BLOCKED()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.Submit(CreateTestAddress(), DateTimeOffset.UtcNow);

        var attempt = EnterpriseCommerce.Domain.Payments.PaymentAttempt.Create(
            order.Id,
            order.TotalAmount,
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await using (var db = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Act 1: Webhook commits Paid first
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var webhookCommand = new EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook.ProcessPaymentWebhookCommand(
                attempt.Id.Value,
                "ECPay",
                "evt-" + Guid.NewGuid().ToString("N"),
                "tx-" + Guid.NewGuid().ToString("N"),
                100m,
                "TWD",
                true);

            var webhookResult = await sender.Send(webhookCommand);
            webhookResult.IsSuccess.Should().BeTrue();
        }

        // Act 2: Customer attempts to cancel Paid order via HTTP
        var client = CreateCustomerClient(customerId: customerId);
        var cancelResponse = await client.PutAsync($"/api/v1/orders/{order.Id.Value}/cancel", null);

        // Assert
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var verifyDb = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Paid, "Paid order must remain Paid and rejection must prevent cancellation");
        }
    }
}
