using System;
using System.Linq;
using System.Text.Json;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

public class TestFailingEventPublisher : IEventPublisher
{
    public bool ShouldThrow { get; set; } = true;
    public bool WasPublished { get; private set; }

    public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("External broker unreachable (simulated failure).");
        }

        WasPublished = true;
        return Task.CompletedTask;
    }
}

[Collection("IntegrationTests")]
public class OutboxRedeliveryIntegrationTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private TestFailingEventPublisher _publisher = null!;

    public OutboxRedeliveryIntegrationTests(MySqlFixture mySqlFixture)
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

        _publisher = new TestFailingEventPublisher();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEventPublisher>(_publisher);
            });
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Outbox_ExternalPublishFailsThenRetries_DomainEventReplayIsIdempotent_AndInventoryNotReleasedTwice()
    {
        // Arrange
        var scope = _factory!.Services.CreateScope();
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
        order.Submit(DateTimeOffset.UtcNow);
        inventoryItem.ReserveStock(new OrderReference(order.Id.Value), new StockQuantity(2));
        db.Orders.Add(order);

        await db.SaveChangesAsync();

        // 3. Cancel the Order -> will raise OrderStatusChangedDomainEvent in Outbox
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
            outboxMsgs.Should().NotBeEmpty();
        }

        // Act 1 - Outbox Attempt 1: Publisher fails
        _publisher.ShouldThrow = true;
        var service = new OutboxBackgroundService(_factory.Services, NullLogger<OutboxBackgroundService>.Instance);
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert 1: In-process domain event released the reservation, but Outbox message is NOT marked processed
        using (var verifyScope1 = _factory.Services.CreateScope())
        {
            var verifyDb1 = verifyScope1.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var msg1 = await verifyDb1.OutboxMessages
                .FirstAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains("\"NewStatus\":4"));
            msg1.ProcessedOn.Should().BeNull("Attempt 1 failed external publish");
            msg1.Error.Should().Contain("External broker unreachable");

            var inv1 = await verifyDb1.InventoryItems.Include(i => i.Reservations).FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv1.AvailableQuantity.Value.Should().Be(10, "Reservation of 2 units was released back to available stock");
            inv1.ReservedQuantity.Value.Should().Be(0);
        }

        // Act 2 - Outbox Attempt 2 (Retry): Publisher now succeeds
        _publisher.ShouldThrow = false;
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert 2: In-process domain event runs again, but inventory release is idempotent; stock is NOT released a second time
        using (var verifyScope2 = _factory.Services.CreateScope())
        {
            var verifyDb2 = verifyScope2.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var msg2 = await verifyDb2.OutboxMessages
                .FirstAsync(m => m.EventType == nameof(OrderStatusChangedDomainEvent) && m.Content.Contains("\"NewStatus\":4"));
            msg2.ProcessedOn.Should().NotBeNull("Attempt 2 succeeded");
            msg2.Error.Should().BeNull();

            var inv2 = await verifyDb2.InventoryItems.Include(i => i.Reservations).FirstAsync(i => i.ProductReference == new ProductReference(productId));
            inv2.AvailableQuantity.Value.Should().Be(10, "Stock must NOT be released twice (must remain exactly 10, not 12)");
            inv2.ReservedQuantity.Value.Should().Be(0, "Reserved quantity must remain 0");
        }
    }
}
