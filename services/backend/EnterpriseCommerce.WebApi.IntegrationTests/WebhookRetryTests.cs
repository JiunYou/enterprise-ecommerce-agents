using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

public class ConcurrencyThrowingInterceptor : SaveChangesInterceptor
{
    public bool ShouldThrow { get; set; } = true;
    private bool _hasThrown;

    public void Reset()
    {
        _hasThrown = false;
        ShouldThrow = true;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrow && !_hasThrown)
        {
            _hasThrown = true;
            throw new DbUpdateConcurrencyException("Optimistic concurrency race simulated via fault injection.");
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

[Collection("IntegrationTests")]
public class WebhookRetryTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private ConcurrencyThrowingInterceptor _interceptor = null!;

    public WebhookRetryTests(MySqlFixture mySqlFixture)
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

        _interceptor = new ConcurrencyThrowingInterceptor { ShouldThrow = false };

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentProvider, EnterpriseCommerce.WebApi.IntegrationTests.Payments.DummyPaymentProvider>();
                services.AddSingleton<ISaveChangesInterceptor>(_interceptor);
            });
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Webhook_AmountMismatch_RollsBack_RetryWithCorrectAmount_Succeeds()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        // Act 1: Amount mismatch
        var providerEventId = Guid.NewGuid().ToString();
        var cmdMismatch = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", providerEventId, Guid.NewGuid().ToString(), 99m, "USD", true);

        using var scope1 = _factory!.Services.CreateScope();
        var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
        var result1 = await sender1.Send(cmdMismatch);

        // Assert 1
        result1.IsFailure.Should().BeTrue();
        result1.Error.Code.Should().Be("Payment.AmountMismatch");

        using var verifyScope1 = _factory!.Services.CreateScope();
        var db1 = verifyScope1.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var attemptState1 = await db1.PaymentAttempts.FirstAsync();
        attemptState1.Status.Should().Be(PaymentAttemptStatus.Pending, "Rollback left it pending");
        var receipts1 = await db1.PaymentWebhookReceipts.CountAsync();
        receipts1.Should().Be(0, "Receipt rolled back");

        // Act 2: Correct amount
        var cmdCorrect = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", providerEventId, Guid.NewGuid().ToString(), 100m, "USD", true);

        using var scope2 = _factory!.Services.CreateScope();
        var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
        var result2 = await sender2.Send(cmdCorrect);

        // Assert 2
        result2.IsSuccess.Should().BeTrue();

        using var verifyScope2 = _factory!.Services.CreateScope();
        var db2 = verifyScope2.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var attemptState2 = await db2.PaymentAttempts.FirstAsync();
        attemptState2.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        var receipts2 = await db2.PaymentWebhookReceipts.CountAsync();
        receipts2.Should().Be(1);
    }

    [Fact]
    public async Task Webhook_CurrencyMismatch_RollsBack_LeavesAttemptPendingAndOrderSubmitted()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        // Act: Currency mismatch (EUR instead of USD)
        var providerEventId = Guid.NewGuid().ToString();
        var cmdCurrencyMismatch = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", providerEventId, Guid.NewGuid().ToString(), 100m, "EUR", true);

        using var actScope = _factory!.Services.CreateScope();
        var result = await actScope.ServiceProvider.GetRequiredService<ISender>().Send(cmdCurrencyMismatch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.CurrencyMismatch");

        using var verifyScope = _factory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        var attemptState = await verifyDb.PaymentAttempts.FirstAsync();
        attemptState.Status.Should().Be(PaymentAttemptStatus.Pending, "Rollback must leave payment attempt pending");

        var orderState = await verifyDb.Orders.FirstAsync();
        orderState.Status.Should().Be(OrderStatus.Submitted, "Order must NOT transition to Paid");

        var receipts = await verifyDb.PaymentWebhookReceipts.CountAsync();
        receipts.Should().Be(0, "Receipt must be rolled back on currency mismatch");
    }

    [Fact]
    public async Task ConcurrentDuplicate_SameProviderAndEventId_HandledIdempotently_WithSingleDurableReceipt()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        var sharedEventId = Guid.NewGuid().ToString();
        var txId = Guid.NewGuid().ToString();
        var command = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", sharedEventId, txId, 100m, "USD", true);

        // Act: Run 2 concurrent processing tasks with the exact same (Provider, ProviderEventId)
        var tasks = new List<Task<EnterpriseCommerce.Domain.Primitives.Result>>();
        for (int i = 0; i < 2; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var runScope = _factory!.Services.CreateScope();
                var sender = runScope.ServiceProvider.GetRequiredService<ISender>();
                return await sender.Send(command);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: Both tasks handle it gracefully without uncaught 500 exceptions
        results.All(r => r.IsSuccess).Should().BeTrue("Duplicate event must be handled idempotently");

        using var verifyScope = _factory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var receipts = await verifyDb.PaymentWebhookReceipts.CountAsync();
        receipts.Should().Be(1, "Exactly one durable receipt must be stored");

        var attemptFinal = await verifyDb.PaymentAttempts.FirstAsync();
        attemptFinal.Status.Should().Be(PaymentAttemptStatus.Succeeded, "Payment must transition to Succeeded");

        var orderFinal = await verifyDb.Orders.FirstAsync();
        orderFinal.Status.Should().Be(OrderStatus.Paid, "Order must transition to Paid");
    }

    [Fact]
    public async Task Webhook_WhenOrderIsCancelled_TransitionsAttemptToRefundRequired_AndOrderRemainsCancelled()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        order.Cancel(); // Order is now cancelled
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        // Act: Webhook delivers successful payment for this cancelled order
        var providerEventId = Guid.NewGuid().ToString();
        var txId = Guid.NewGuid().ToString();
        var cmd = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", providerEventId, txId, 100m, "USD", true);

        using var actScope = _factory!.Services.CreateScope();
        var result = await actScope.ServiceProvider.GetRequiredService<ISender>().Send(cmd);

        // Assert
        result.IsSuccess.Should().BeTrue();

        using var verifyScope = _factory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        var attemptFinal = await verifyDb.PaymentAttempts.FirstAsync();
        attemptFinal.Status.Should().Be(PaymentAttemptStatus.RefundRequired, "Late payment on cancelled order must transition to RefundRequired");

        var orderFinal = await verifyDb.Orders.FirstAsync();
        orderFinal.Status.Should().Be(OrderStatus.Cancelled, "Cancelled order must remain non-Paid");

        var receipts = await verifyDb.PaymentWebhookReceipts.CountAsync();
        receipts.Should().Be(1, "Payment webhook receipt must be durably recorded");
    }

    [Fact]
    public async Task Webhook_OptimisticConcurrencyConflict_RollsBack_AndRetryConvergesToRefundRequired()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        var providerEventId = Guid.NewGuid().ToString();
        var txId = Guid.NewGuid().ToString();
        var cmd = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", providerEventId, txId, 100m, "USD", true);

        // Act 1: Simulate losing optimistic concurrency race during SaveChangesAsync via fault-injection interceptor
        _interceptor.Reset();
        _interceptor.ShouldThrow = true;

        using (var actScope1 = _factory!.Services.CreateScope())
        {
            var sender1 = actScope1.ServiceProvider.GetRequiredService<ISender>();
            Func<Task> act = async () => await sender1.Send(cmd);
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }

        // Assert 1: Entire transaction rolled back
        using (var verifyScope1 = _factory!.Services.CreateScope())
        {
            var verifyDb1 = verifyScope1.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var attemptState1 = await verifyDb1.PaymentAttempts.FirstAsync();
            attemptState1.Status.Should().Be(PaymentAttemptStatus.Pending, "Losing race must leave attempt pending");

            var orderState1 = await verifyDb1.Orders.FirstAsync();
            orderState1.Status.Should().Be(OrderStatus.Submitted, "Order must not be marked Paid on rollback");

            var receipts1 = await verifyDb1.PaymentWebhookReceipts.CountAsync();
            receipts1.Should().Be(0, "Receipt must be absent after rollback");

            var outboxPaidEvents = await verifyDb1.OutboxMessages
                .Where(m => m.Content.Contains("\"NewStatus\":3")) // Paid
                .CountAsync();
            outboxPaidEvents.Should().Be(0, "Losing Outbox writes must be absent");
        }

        // Mid-flight event: Order is cancelled/expired concurrently
        using (var cancelScope = _factory!.Services.CreateScope())
        {
            var cancelDb = cancelScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var orderToCancel = await cancelDb.Orders.FirstAsync();
            orderToCancel.Cancel();
            await cancelDb.SaveChangesAsync();
        }

        // Act 2: Webhook retries and reloads current Order state
        _interceptor.ShouldThrow = false;
        using (var actScope2 = _factory!.Services.CreateScope())
        {
            var sender2 = actScope2.ServiceProvider.GetRequiredService<ISender>();
            var result2 = await sender2.Send(cmd);
            result2.IsSuccess.Should().BeTrue();
        }

        // Assert 2: Converges to RefundRequired
        using (var verifyScope2 = _factory!.Services.CreateScope())
        {
            var verifyDb2 = verifyScope2.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var attemptFinal = await verifyDb2.PaymentAttempts.FirstAsync();
            attemptFinal.Status.Should().Be(PaymentAttemptStatus.RefundRequired);

            var orderFinal = await verifyDb2.Orders.FirstAsync();
            orderFinal.Status.Should().Be(OrderStatus.Cancelled);

            var receiptsFinal = await verifyDb2.PaymentWebhookReceipts.CountAsync();
            receiptsFinal.Should().Be(1, "Durable receipt persisted upon retry convergence");
        }
    }

    [Fact]
    public async Task SemanticDuplicate_DistinctEventId_SameTxId_AcknowledgedSafely()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        db.Orders.Add(order);

        var attempt = PaymentAttempt.Create(order.Id, order.TotalAmount, "dummy_provider", Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.PaymentAttempts.Add(attempt);
        await db.SaveChangesAsync();
        scope.Dispose();

        // Act 1: First Event
        var eventA = Guid.NewGuid().ToString();
        var txId = Guid.NewGuid().ToString();
        var cmdA = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", eventA, txId, 100m, "USD", true);

        using var scope1 = _factory!.Services.CreateScope();
        var result1 = await scope1.ServiceProvider.GetRequiredService<ISender>().Send(cmdA);

        // Act 2: Semantic duplicate (different event, same transaction/attempt)
        var eventB = Guid.NewGuid().ToString();
        var cmdB = new ProcessPaymentWebhookCommand(
            attempt.Id.Value, "dummy_provider", eventB, txId, 100m, "USD", true);

        using var scope2 = _factory!.Services.CreateScope();
        var result2 = await scope2.ServiceProvider.GetRequiredService<ISender>().Send(cmdB);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();

        using var verifyScope = _factory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        var receipts = await verifyDb.PaymentWebhookReceipts.CountAsync();
        receipts.Should().Be(2, "Both distinct events are acknowledged and recorded");

        var finalOrder = await verifyDb.Orders.FirstAsync();
        finalOrder.Status.Should().Be(OrderStatus.Paid);
        
        var attemptCount = await verifyDb.PaymentAttempts.CountAsync();
        attemptCount.Should().Be(1, "No dual payment effect occurred");
    }
}
