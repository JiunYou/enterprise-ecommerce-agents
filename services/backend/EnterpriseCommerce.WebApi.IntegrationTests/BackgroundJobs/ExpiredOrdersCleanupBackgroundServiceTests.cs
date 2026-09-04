using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Application.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

namespace EnterpriseCommerce.WebApi.IntegrationTests.BackgroundJobs;

[Collection("IntegrationTests")]
public class ExpiredOrdersCleanupBackgroundServiceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;

    public ExpiredOrdersCleanupBackgroundServiceTests(MySqlFixture mySqlFixture)
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


    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }



    }

    [Fact]
    public async Task ExpiredOrder_ShouldBeCancelled_AndInventoryReleased()
    {
        // 1. Arrange factory to poll very fast (e.g., 2 seconds) and consider expiration window 0 for test




        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.UseSetting("BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes", "0");
            builder.UseSetting("BackgroundJobs:ExpiredOrdersCleanup:PollIntervalSeconds", "2");
            builder.UseSetting("BackgroundJobs:Outbox:PollIntervalSeconds", "2");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IEventPublisher, TestEventPublisher>();
            });
        });

        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);
        var orderId = Guid.NewGuid();

        // 2. Setup Database: Add an inventory item and an already submitted order
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            
            var inventoryItem = InventoryItem.Create(productRef);
            inventoryItem.IncreaseStock(10);
            inventoryItem.ReserveStock(orderId, 5); // Reserve 5 stock for the order
            
            dbContext.InventoryItems.Add(inventoryItem);
            
            var order = Order.Create(Guid.NewGuid(), "TWD");
            
            // Set order ID
            var t = typeof(Order);
            var idProp = t.GetProperty("Id");
            idProp!.SetValue(order, new OrderId(orderId));
            
            order.AddItem(new ProductId(productId), new Money(100, "TWD"), 5);
            
            var shippingAddress = ShippingAddress.Create("Test Customer", "0912345678", "TW", "100", "Taipei", "123 Main St").Value;
            var submitResult = order.Submit(shippingAddress, DateTimeOffset.UtcNow.AddMinutes(-1)); // Submitted 1 min ago, so it's expired
            
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        // 3. Act: Give the background service time to poll and process
        // We create a client to ensure the hosted services are fully started
        using var client = _factory.CreateClient();
        
        // 4. Assert with retry (OutboxBackgroundService polls every 5 seconds)
        bool success = false;
        for (int i = 0; i < 15; i++)
        {
            await Task.Delay(1000);
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            
            var updatedOrder = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == new OrderId(orderId));
            var updatedInventory = await dbContext.InventoryItems.FirstOrDefaultAsync(inv => inv.ProductReference == productRef);

            if (updatedOrder != null && updatedOrder.Status == OrderStatus.Cancelled && updatedInventory != null && updatedInventory.AvailableQuantity.Value == 10)
            {
                success = true;
                break;
            }
            
            await Task.Delay(1000);
        }

        success.Should().BeTrue("Order should be cancelled and inventory released within 15 seconds");
    }
}
