using Microsoft.AspNetCore.TestHost;
using EnterpriseCommerce.Application.Payments;

using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

using System.Diagnostics;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Payments;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.BackgroundJobs;

[Collection("IntegrationTests")]
public class PaymentConcurrencyTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public PaymentConcurrencyTests(MySqlFixture mySqlFixture)
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
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentProvider, EnterpriseCommerce.WebApi.IntegrationTests.Payments.DummyPaymentProvider>();
            });
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitiatePayment_ConcurrentRequests_SameIdempotencyKey_SafelyReusesSinglePendingAttempt_AndProducesSingleAttemptInDb()
    {
        // 1. Arrange a real database and a Submitted Order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // 2. Act: Send 5 concurrent initiation requests with the SAME idempotency key
        var sharedIdempotencyKey = Guid.NewGuid();
        var tasks = new List<Task<Domain.Primitives.Result<InitiatePaymentResponse>>>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var innerScope = _factory.Services.CreateScope();
                var innerSender = innerScope.ServiceProvider.GetRequiredService<ISender>();
                var command = new InitiatePaymentCommand(order.Id.Value, sharedIdempotencyKey, order.CustomerId);
                return await innerSender.Send(command);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 3. Assert: All succeed by safely reusing the single attempt under pessimistic lock
        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => r.IsFailure);

        successes.Should().Be(5, "Because same idempotency key safely reuses the pending attempt under pessimistic lock");
        failures.Should().Be(0);

        results.Select(r => r.Value.ProviderTransactionId).Distinct().Should().ContainSingle("All callers with the same idempotency key receive the exact same provider transaction identity");

        var attemptCount = await db.PaymentAttempts.CountAsync();
        attemptCount.Should().Be(1, "Exactly one PaymentAttempt row must be persisted in database for same idempotency key");
    }

    [Fact]
    public async Task InitiatePayment_ConcurrentRequests_DifferentIdempotencyKeys_CreatesMultiplePaymentAttempts()
    {
        // 1. Arrange a real database and a Submitted Order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // 2. Act: Send 5 concurrent initiation requests with different idempotency keys
        var tasks = new List<Task<Domain.Primitives.Result<InitiatePaymentResponse>>>();

        for (int i = 0; i < 5; i++)
        {
            var uniqueIdempotencyKey = Guid.NewGuid();
            tasks.Add(Task.Run(async () =>
            {
                using var innerScope = _factory.Services.CreateScope();
                var innerSender = innerScope.ServiceProvider.GetRequiredService<ISender>();
                var command = new InitiatePaymentCommand(order.Id.Value, uniqueIdempotencyKey, order.CustomerId);
                return await innerSender.Send(command);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 3. Assert: Each distinct idempotency key creates a distinct PaymentAttempt
        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => r.IsFailure);

        successes.Should().Be(5, "Because each distinct idempotency key represents a distinct attempt and succeeds");
        failures.Should().Be(0);

        results.Select(r => r.Value.ProviderTransactionId).Distinct().Should().HaveCount(5, "Each different-key caller should receive a distinct provider transaction identity");

        var attemptCount = await db.PaymentAttempts.CountAsync();
        attemptCount.Should().Be(5, "Exactly 5 PaymentAttempt rows must be persisted for 5 distinct idempotency keys");
    }

    [Fact]
    public async Task OverlappingPaymentAttempts_LateSuccess_AThenB_MarksSecondRefundRequired()
    {
        // 1. Arrange a real database and a Submitted Order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Act 1: Initiate with Key A
        var keyA = Guid.NewGuid();
        var resA = await sender.Send(new InitiatePaymentCommand(order.Id.Value, keyA, order.CustomerId));
        resA.IsSuccess.Should().BeTrue();

        // Act 2: Customer retries with Key B (abandon-style retry)
        var keyB = Guid.NewGuid();
        var resB = await sender.Send(new InitiatePaymentCommand(order.Id.Value, keyB, order.CustomerId));
        resB.IsSuccess.Should().BeTrue();

        // Verify both are Pending in DB
        var attemptA = await db.PaymentAttempts.FirstAsync(pa => pa.IdempotencyKey == keyA);
        var attemptB = await db.PaymentAttempts.FirstAsync(pa => pa.IdempotencyKey == keyB);
        attemptA.Status.Should().Be(PaymentAttemptStatus.Pending);
        attemptB.Status.Should().Be(PaymentAttemptStatus.Pending);
        attemptA.Id.Should().NotBe(attemptB.Id);

        // Act 3: A succeeds first
        var webhookA = new ProcessPaymentWebhookCommand(
            attemptA.Id.Value,
            "dummy_provider",
            "evt_A_123",
            "tx_A_456",
            100m,
            "USD",
            true);
        var webhookResA = await sender.Send(webhookA);
        webhookResA.IsSuccess.Should().BeTrue();

        // Verify Order is Paid, A is Succeeded
        await db.Entry(order).ReloadAsync();
        await db.Entry(attemptA).ReloadAsync();
        order.Status.Should().Be(OrderStatus.Paid);
        attemptA.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        attemptA.ProviderTransactionId.Should().Be("tx_A_456");

        // Act 4: B subsequently succeeds
        var webhookB = new ProcessPaymentWebhookCommand(
            attemptB.Id.Value,
            "dummy_provider",
            "evt_B_789",
            "tx_B_999",
            100m,
            "USD",
            true);
        var webhookResB = await sender.Send(webhookB);
        webhookResB.IsSuccess.Should().BeTrue();

        // Verify Order remains Paid, B is RefundRequired
        await db.Entry(order).ReloadAsync();
        await db.Entry(attemptB).ReloadAsync();
        order.Status.Should().Be(OrderStatus.Paid);
        attemptB.Status.Should().Be(PaymentAttemptStatus.RefundRequired);
        attemptB.ProviderTransactionId.Should().Be("tx_B_999");

        // Receipt count is 2
        var receiptCount = await db.PaymentWebhookReceipts.CountAsync();
        receiptCount.Should().Be(2);
    }

    [Fact]
    public async Task OverlappingPaymentAttempts_LateSuccess_BThenA_MarksSecondRefundRequired()
    {
        // 1. Arrange a real database and a Submitted Order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Act 1: Initiate with Key A
        var keyA = Guid.NewGuid();
        var resA = await sender.Send(new InitiatePaymentCommand(order.Id.Value, keyA, order.CustomerId));
        resA.IsSuccess.Should().BeTrue();

        // Act 2: Customer retries with Key B
        var keyB = Guid.NewGuid();
        var resB = await sender.Send(new InitiatePaymentCommand(order.Id.Value, keyB, order.CustomerId));
        resB.IsSuccess.Should().BeTrue();

        var attemptA = await db.PaymentAttempts.FirstAsync(pa => pa.IdempotencyKey == keyA);
        var attemptB = await db.PaymentAttempts.FirstAsync(pa => pa.IdempotencyKey == keyB);

        // Act 3: B succeeds first
        var webhookB = new ProcessPaymentWebhookCommand(
            attemptB.Id.Value,
            "dummy_provider",
            "evt_B_789",
            "tx_B_999",
            100m,
            "USD",
            true);
        var webhookResB = await sender.Send(webhookB);
        webhookResB.IsSuccess.Should().BeTrue();

        // Verify Order is Paid, B is Succeeded
        await db.Entry(order).ReloadAsync();
        await db.Entry(attemptB).ReloadAsync();
        order.Status.Should().Be(OrderStatus.Paid);
        attemptB.Status.Should().Be(PaymentAttemptStatus.Succeeded);

        // Act 4: A subsequently succeeds
        var webhookA = new ProcessPaymentWebhookCommand(
            attemptA.Id.Value,
            "dummy_provider",
            "evt_A_123",
            "tx_A_456",
            100m,
            "USD",
            true);
        var webhookResA = await sender.Send(webhookA);
        webhookResA.IsSuccess.Should().BeTrue();

        // Verify Order remains Paid, A is RefundRequired
        await db.Entry(order).ReloadAsync();
        await db.Entry(attemptA).ReloadAsync();
        order.Status.Should().Be(OrderStatus.Paid);
        attemptA.Status.Should().Be(PaymentAttemptStatus.RefundRequired);

        // Receipt count is 2
        var receiptCount = await db.PaymentWebhookReceipts.CountAsync();
        receiptCount.Should().Be(2);
    }

    private static ShippingAddress CreateTestShippingAddress()
    {
        return ShippingAddress.Create("Test Customer", "0912345678", "TW", "100", "Taipei", "123 Main St").Value;
    }
}
