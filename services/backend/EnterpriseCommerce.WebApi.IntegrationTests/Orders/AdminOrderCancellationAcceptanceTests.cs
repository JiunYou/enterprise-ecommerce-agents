using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;
using EnterpriseCommerce.Application.Orders.Commands.CancelOrder;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Orders;
using EnterpriseCommerce.Infrastructure.Persistence.Outbox;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class AdminOrderCancellationAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminOrderCancellationAcceptanceTests(MySqlFixture mySqlFixture)
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
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, _ => { });

                services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
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

    private HttpClient CreateAdminClient(string sub = "auth0|admin-acceptance", string issuer = "https://auth.enterprisecommerce.com/")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
        return client;
    }

    private static ShippingAddress CreateTestShippingAddress()
    {
        return ShippingAddress.Create("Acceptance Customer", "0912345678", "TW", "100", "Taipei", "123 Test Rd").Value;
    }

    private async Task ClearPendingOutboxMessagesAsync()
    {
        await using var db = CreateFreshDbContext();
        await db.Database.ExecuteSqlRawAsync("UPDATE OutboxMessages SET ProcessedOn = UTC_TIMESTAMP() WHERE ProcessedOn IS NULL;");
    }

    private async Task ProcessOutboxAsync()
    {
        var service = new OutboxBackgroundService(_factory.Services, NullLogger<OutboxBackgroundService>.Instance);
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int i = 0; i < 5; i++)
        {
            await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;
        }
    }

    private sealed class NoOpEventPublisher : IEventPublisher
    {
        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task ADMIN_CANCEL_PAYMENT_RACE_CANCEL_WINS()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        var attempt = PaymentAttempt.Create(
            order.Id,
            new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Act 1: Admin cancellation commits first
        var client = CreateAdminClient();
        var cancelResponse = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Admin cancelled before payment"));
        cancelResponse.EnsureSuccessStatusCode();

        // Act 2: Process genuine payment webhook success path
        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var webhookCommand = new ProcessPaymentWebhookCommand(
                attempt.Id.Value,
                "ECPay",
                "evt-" + Guid.NewGuid().ToString("N"),
                "tx-" + Guid.NewGuid().ToString("N"),
                100m,
                "USD",
                true);

            var webhookResult = await sender.Send(webhookCommand);
            webhookResult.IsSuccess.Should().BeTrue();
        }

        // Assert
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled, "Order must remain Cancelled and NOT become Paid");

            var finalAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
            finalAttempt.Status.Should().Be(PaymentAttemptStatus.RefundRequired, "Late successful payment must be marked RefundRequired");

            var audit = await verifyDb.AdminOrderCancellations.FirstOrDefaultAsync(a => a.OrderId == order.Id);
            audit.Should().NotBeNull();
            audit!.Reason.Should().Be("Admin cancelled before payment");
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_PAYMENT_RACE_PAYMENT_WINS()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        var attempt = PaymentAttempt.Create(
            order.Id,
            new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"),
            "ECPay",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            db.PaymentAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }

        // Scope 1: Admin cancellation loads Submitted order via DbContext
        var adminScope = _factory.Services.CreateScope();
        var adminDbContext = adminScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var adminAuditStore = adminScope.ServiceProvider.GetRequiredService<IAdminOrderCancellationStore>();
        var adminTimeProvider = adminScope.ServiceProvider.GetRequiredService<TimeProvider>();

        var staleOrder = await adminDbContext.Orders.FirstAsync(o => o.Id == order.Id);
        staleOrder.Status.Should().Be(OrderStatus.Submitted);

        // Scope 2: Payment webhook commits Paid in separate scope
        using (var paymentScope = _factory.Services.CreateScope())
        {
            var sender = paymentScope.ServiceProvider.GetRequiredService<ISender>();
            var webhookCommand = new ProcessPaymentWebhookCommand(
                attempt.Id.Value,
                "ECPay",
                "evt-" + Guid.NewGuid().ToString("N"),
                "tx-" + Guid.NewGuid().ToString("N"),
                100m,
                "USD",
                true);

            var webhookResult = await sender.Send(webhookCommand);
            webhookResult.IsSuccess.Should().BeTrue();
        }

        // Scope 1 attempts to commit stale cancellation
        staleOrder.Cancel();
        adminAuditStore.Add(new AdminOrderCancellationAudit(
            staleOrder.Id.Value,
            "https://auth.enterprisecommerce.com/",
            "auth0|admin-stale",
            adminTimeProvider.GetUtcNow(),
            "Stale cancel attempt"));

        Func<Task> act = async () => await adminDbContext.SaveChangesAsync();

        // Must throw DbUpdateConcurrencyException due to Order.Version mismatch
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        adminScope.Dispose();

        // Assert fresh DB
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Paid);

            var finalAttempt = await verifyDb.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
            finalAttempt.Status.Should().Be(PaymentAttemptStatus.Succeeded);

            var auditExists = await verifyDb.AdminOrderCancellations.AnyAsync(a => a.OrderId == order.Id);
            auditExists.Should().BeFalse("AdminOrderCancellation must NOT exist when payment won");

            // Forbidden outcome assertion: Order cannot be Cancelled while Payment is Succeeded
            (finalOrder.Status == OrderStatus.Cancelled && finalAttempt.Status == PaymentAttemptStatus.Succeeded)
                .Should().BeFalse("Forbidden corrupted state: Order Cancelled and Payment Succeeded");
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_VS_ADMIN_CANCEL()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(50m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Scope 1: Admin 1 cancels and commits
        using (var scope1 = _factory.Services.CreateScope())
        {
            var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
            var cmd1 = new AdminCancelOrderCommand(order.Id.Value, "https://auth.example.com/", "auth0|admin-1", "First cancel");
            var result1 = await sender1.Send(cmd1);
            result1.IsSuccess.Should().BeTrue();
        }

        // Scope 2: Admin 2 attempts cancel on the cancelled order (or stale read)
        using (var scope2 = _factory.Services.CreateScope())
        {
            var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
            var cmd2 = new AdminCancelOrderCommand(order.Id.Value, "https://auth.example.com/", "auth0|admin-2", "Second cancel");
            var result2 = await sender2.Send(cmd2);

            // Must be rejected
            result2.IsFailure.Should().BeTrue();
        }

        // Fresh DB check
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled);

            var audits = await verifyDb.AdminOrderCancellations.Where(a => a.OrderId == order.Id).ToListAsync();
            audits.Should().HaveCount(1, "Exactly one Admin cancellation audit must be persisted");
            audits[0].ActorSubject.Should().Be("auth0|admin-1");
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_VS_CUSTOMER_CANCEL()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(60m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Scope 1: Admin loads Submitted order in its DbContext
        var adminScope = _factory.Services.CreateScope();
        var adminDbContext = adminScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var adminAuditStore = adminScope.ServiceProvider.GetRequiredService<IAdminOrderCancellationStore>();
        var adminTimeProvider = adminScope.ServiceProvider.GetRequiredService<TimeProvider>();

        var staleOrder = await adminDbContext.Orders.FirstAsync(o => o.Id == order.Id);
        staleOrder.Status.Should().Be(OrderStatus.Submitted);

        // Scope 2: Customer cancels order first using existing Customer command
        using (var customerScope = _factory.Services.CreateScope())
        {
            var customerSender = customerScope.ServiceProvider.GetRequiredService<ISender>();
            var customerCmd = new CancelOrderCommand(order.Id.Value, customerId);
            var customerResult = await customerSender.Send(customerCmd);
            customerResult.IsSuccess.Should().BeTrue();
        }

        // Scope 1: Admin attempts to SaveChanges on the stale order
        staleOrder.Cancel();
        adminAuditStore.Add(new AdminOrderCancellationAudit(
            staleOrder.Id.Value,
            "https://auth.example.com/",
            "auth0|admin-loser",
            adminTimeProvider.GetUtcNow(),
            "Admin loses to customer"));

        Func<Task> act = async () => await adminDbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

        adminScope.Dispose();

        // Assert fresh DB
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled);

            var auditExists = await verifyDb.AdminOrderCancellations.AnyAsync(a => a.OrderId == order.Id);
            auditExists.Should().BeFalse("AdminOrderCancellation must NOT exist when customer cancelled first");
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_AUDIT_ATOMICITY()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(70m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        // Pre-insert an existing AdminOrderCancellation row to cause PK violation on OrderId
        var preExistingAudit = AdminOrderCancellation.Create(
            order.Id,
            "https://auth.existing.com/",
            "auth0|existing-audit",
            DateTimeOffset.UtcNow.AddHours(-1),
            "Pre-existing audit");

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            db.AdminOrderCancellations.Add(preExistingAudit);
            await db.SaveChangesAsync();
        }

        int outboxCountBefore;
        await using (var db = CreateFreshDbContext())
        {
            outboxCountBefore = await db.OutboxMessages.CountAsync();
        }

        // Act: Attempt Admin cancellation on this order via API
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Atomicity test"));
        // PK violation causes internal failure
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);

        // Assert: Order cancellation must NOT have committed
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Submitted, "Order must remain in original state if audit fails");

            var audits = await verifyDb.AdminOrderCancellations.Where(a => a.OrderId == order.Id).ToListAsync();
            audits.Should().HaveCount(1, "Audit count must remain the pre-existing row only");
            audits[0].ActorSubject.Should().Be("auth0|existing-audit");

            var outboxCountAfter = await verifyDb.OutboxMessages.CountAsync();
            outboxCountAfter.Should().Be(outboxCountBefore, "No cancellation Outbox message should have been committed");
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_OUTBOX_ATOMICITY()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(80m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act
        var client = CreateAdminClient("auth0|admin-outbox");
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Atomic outbox test"));
        response.EnsureSuccessStatusCode();

        // Assert
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled);

            var audit = await verifyDb.AdminOrderCancellations.FirstOrDefaultAsync(a => a.OrderId == order.Id);
            audit.Should().NotBeNull();
            audit!.ActorSubject.Should().Be("auth0|admin-outbox");

            var outboxMsg = await verifyDb.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) &&
                                          m.Content.Contains(order.Id.Value.ToString()) &&
                                          m.Content.Contains("\"NewStatus\":4"));
            outboxMsg.Should().NotBeNull("Unprocessed Outbox message must be atomically committed with Order and Audit");
            outboxMsg!.ProcessedOn.Should().BeNull();
        }
    }

    [Fact]
    public async Task SUBMITTED_INVENTORY_RELEASE()
    {
        // Arrange
        await ClearPendingOutboxMessagesAsync();

        var productId = Guid.NewGuid();
        var inventory = InventoryItem.Create(new ProductReference(productId));
        inventory.IncreaseStock(new StockQuantity(10));

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(productId), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(30m, "USD"), 2);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        inventory.ReserveStock(new OrderReference(order.Id.Value), new StockQuantity(2));

        await using (var db = CreateFreshDbContext())
        {
            db.InventoryItems.Add(inventory);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Verify initial state: Available 8, Reserved 2
        await using (var checkDb = CreateFreshDbContext())
        {
            var inv = await checkDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv.AvailableQuantity.Value.Should().Be(8);
            inv.ReservedQuantity.Value.Should().Be(2);
        }

        // Act: Admin cancellation
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Release submitted reservation"));
        response.EnsureSuccessStatusCode();

        // Process Outbox (eventual release)
        await ProcessOutboxAsync();

        // Assert: Available 10, Reserved 0
        await using (var verifyDb = CreateFreshDbContext())
        {
            var inv = await verifyDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv.AvailableQuantity.Value.Should().Be(10, "Stock was released back to available");
            inv.ReservedQuantity.Value.Should().Be(0);
        }
    }

    [Fact]
    public async Task PENDING_CANCEL_INVENTORY_SAFE()
    {
        // Arrange: Pending order without reservation
        await ClearPendingOutboxMessagesAsync();

        var productId = Guid.NewGuid();
        var inventory = InventoryItem.Create(new ProductReference(productId));
        inventory.IncreaseStock(new StockQuantity(10));

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(productId), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(30m, "USD"), 2);
        // Do not submit -> remains Pending, no reservation made

        await using (var db = CreateFreshDbContext())
        {
            db.InventoryItems.Add(inventory);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act: Admin cancels Pending order
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Cancel pending order"));
        response.EnsureSuccessStatusCode();

        // Process Outbox
        await ProcessOutboxAsync();

        // Assert: Stock unharmed
        await using (var verifyDb = CreateFreshDbContext())
        {
            var finalOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            finalOrder.Status.Should().Be(OrderStatus.Cancelled);

            var inv = await verifyDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv.AvailableQuantity.Value.Should().Be(10, "Stock quantity must remain unchanged");
            inv.ReservedQuantity.Value.Should().Be(0);
        }
    }

    [Fact]
    public async Task OUTBOX_REDELIVERY_NO_OVER_RELEASE()
    {
        // Arrange
        await ClearPendingOutboxMessagesAsync();

        var productId = Guid.NewGuid();
        var inventory = InventoryItem.Create(new ProductReference(productId));
        inventory.IncreaseStock(new StockQuantity(10));

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(productId), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(30m, "USD"), 3);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        inventory.ReserveStock(new OrderReference(order.Id.Value), new StockQuantity(3));

        await using (var db = CreateFreshDbContext())
        {
            db.InventoryItems.Add(inventory);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act: Cancel order
        var client = CreateAdminClient();
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{order.Id.Value}/cancel", new AdminCancelOrderRequest("Redelivery test"));
        response.EnsureSuccessStatusCode();

        // Outbox delivery 1
        await ProcessOutboxAsync();

        // Assert 1: Available 10, Reserved 0
        await using (var checkDb = CreateFreshDbContext())
        {
            var inv = await checkDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv.AvailableQuantity.Value.Should().Be(10);
            inv.ReservedQuantity.Value.Should().Be(0);
        }

        // Simulate redelivery: Reset Outbox message ProcessedOn to null
        await using (var resetDb = CreateFreshDbContext())
        {
            var msg = await resetDb.OutboxMessages.FirstAsync(m => m.Content.Contains(order.Id.Value.ToString()));
            msg.ProcessedOn = null;
            await resetDb.SaveChangesAsync();
        }

        // Outbox delivery 2 (Redelivery)
        await ProcessOutboxAsync();

        // Assert 2: Still Available 10, Reserved 0 (NOT 13!)
        await using (var verifyDb = CreateFreshDbContext())
        {
            var inv = await verifyDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv.AvailableQuantity.Value.Should().Be(10, "Stock must NOT be over-released on redelivery");
            inv.ReservedQuantity.Value.Should().Be(0);
        }
    }

    [Fact]
    public async Task ADMIN_CANCEL_REAL_EF_CONCURRENCY_EXCEPTION_ACTUAL_HANDLER_HTTP_409()
    {
        // Arrange: 建立真實 Submitted 訂單寫入 MySQL
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(99m, "USD"), 1);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);

        await using (var db = CreateFreshDbContext())
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // 僅替換 IApplicationUnitOfWork，保留真實 Order/Store/Controller/MediatR/Handler Pipeline
        using var concurrencyFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationUnitOfWork));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IApplicationUnitOfWork, RealEfConcurrencyThrowingUnitOfWork>();
            });
        });

        var client = concurrencyFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-Sub", "auth0|admin-concurrency");
        client.DefaultRequestHeaders.Add("X-Test-Issuer", "https://auth.enterprisecommerce.com/");

        // Act: 呼叫真實端點，走完整 Controller -> MediatR -> AdminCancelOrderCommandHandler，不 mock ISender
        var response = await client.PutAsJsonAsync(
            $"/api/v1/admin/orders/{order.Id.Value}/cancel",
            new AdminCancelOrderRequest("Real EF Concurrency Conflict Test"));

        // Assert 1: HTTP 回應必須為 409 Conflict
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert 2: 查詢全新的 DbContext，確認 Order 維持原狀態且未產生審計紀錄
        await using (var verifyDb = CreateFreshDbContext())
        {
            var currentOrder = await verifyDb.Orders.FirstAsync(o => o.Id == order.Id);
            currentOrder.Status.Should().Be(OrderStatus.Submitted, "Order must remain in original state when concurrency exception occurs.");
            currentOrder.Version.Should().Be(0);

            var auditCount = await verifyDb.AdminOrderCancellations.CountAsync(a => a.OrderId == order.Id);
            auditCount.Should().Be(0, "No AdminOrderCancellation audit should be persisted upon concurrency conflict.");
        }
    }

    private sealed class RealEfConcurrencyThrowingUnitOfWork : IApplicationUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Simulated real EF Core concurrency conflict.");
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
