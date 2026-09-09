using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Outbox;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Outbox;

[Collection("IntegrationTests")]
public class OutboxLocalDispatchMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public OutboxLocalDispatchMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
                // Disable the background hosted service execution inside test host for deterministic invocation
                var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(OutboxBackgroundService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                // Strictly adhere to production DI: NO fake publisher, NO rabbitmq, NO notification worker
            });
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Outbox_WithProductionDI_LocalDispatchSucceeds_AndOutboxIsMarkedProcessedWithoutExternalPublisher()
    {
        // Verify production DI invariant: IEventPublisher is NOT registered
        _factory.Services.GetService<IEventPublisher>().Should().BeNull("Production DI does not register IEventPublisher");

        // Arrange
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var productId = Guid.NewGuid();

        // 1. Setup Inventory with 10 total (8 available, 2 reserved)
        var inventoryItem = InventoryItem.Create(new ProductReference(productId));
        inventoryItem.IncreaseStock(new StockQuantity(10));
        db.InventoryItems.Add(inventoryItem);

        // 2. Create and Submit Order (which reserves 2 units)
        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(productId), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(50m, "USD"), 2);
        order.Submit(CreateTestShippingAddress(), DateTimeOffset.UtcNow);
        inventoryItem.ReserveStock(new OrderReference(order.Id.Value), new StockQuantity(2));
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        // 3. Cancel the Order -> creates recognized OrderStatusChangedDomainEvent in Outbox
        order.Cancel();
        await db.SaveChangesAsync();
        scope.Dispose();

        // Ensure Outbox has the OrderStatusChangedDomainEvent for Cancelled
        using (var checkScope = _factory.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var outboxMsgs = await checkDb.OutboxMessages
                .Where(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains("\"NewStatus\":4"))
                .ToListAsync();
            outboxMsgs.Should().HaveCount(1);
            outboxMsgs[0].ProcessedOn.Should().BeNull();
        }

        // Act 1 - Invoke Outbox processor once
        var service = new OutboxBackgroundService(_factory.Services, NullLogger<OutboxBackgroundService>.Instance);
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert 1: Future contract requires local side-effect executed AND Outbox marked processed
        using (var verifyScope1 = _factory.Services.CreateScope())
        {
            var verifyDb1 = verifyScope1.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            
            // Verify local side effect occurred
            var inv1 = await verifyDb1.InventoryItems.Include(i => i.Reservations).FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv1.AvailableQuantity.Value.Should().Be(10, "Reservation of 2 units was released back to available stock by local domain event handler");
            inv1.ReservedQuantity.Value.Should().Be(0);

            // Verify Outbox state under Future Local-Dispatch-Only contract
            var msg1 = await verifyDb1.OutboxMessages
                .FirstAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains("\"NewStatus\":4"));
            
            msg1.ProcessedOn.Should().NotBeNull("Future local-dispatch-only contract requires OutboxMessage to be marked processed once local dispatch succeeds, even without IEventPublisher");
            msg1.Error.Should().BeNull();
        }

        // Act 2 - Invoke Outbox processor second time (Subsequent Run)
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert 2: Message is not replayed, inventory remains unchanged
        using (var verifyScope2 = _factory.Services.CreateScope())
        {
            var verifyDb2 = verifyScope2.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var inv2 = await verifyDb2.InventoryItems.Include(i => i.Reservations).FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv2.AvailableQuantity.Value.Should().Be(10, "Stock must remain exactly 10 (not released multiple times)");
            inv2.ReservedQuantity.Value.Should().Be(0);

            var msg2 = await verifyDb2.OutboxMessages
                .FirstAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains("\"NewStatus\":4"));
            msg2.ProcessedOn.Should().NotBeNull();
            msg2.Error.Should().BeNull();
        }
    }

    private static ShippingAddress CreateTestShippingAddress()
    {
        return ShippingAddress.Create("Test Customer", "0912345678", "TW", "100", "Taipei", "123 Main St").Value;
    }
}
