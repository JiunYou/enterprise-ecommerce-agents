using EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Testcontainers.MySql;
using Xunit;

using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class ConcurrencyTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;

    public ConcurrencyTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {

        // 1. Run migrations manually so the DB is ready
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }


        // We use a custom factory that does NOT mock MediatR (ISender).
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
                // We keep the real ISender
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

    [Fact]
    public async Task SubmitOrder_With100ConcurrentSubmissionsForStock10_Exactly10Succeed()
    {
        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);
        int initialStock = 10;
        int concurrentCount = 100;

        // 1. Arrange: Create stock 10
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var inventoryItem = InventoryItem.Create(productRef);
            inventoryItem.IncreaseStock(initialStock);
            dbContext.InventoryItems.Add(inventoryItem);
            await dbContext.SaveChangesAsync();
        }

        var orderIds = new List<Guid>();
        var orderToCustomer = new Dictionary<Guid, Guid>();

        // Setup 100 pending orders, each with 1 quantity of the product
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            for (int i = 0; i < concurrentCount; i++)
            {
                var customerId = Guid.NewGuid();
                var order = Order.Create(customerId, "TWD");
                order.AddItem(new ProductId(productId), new Money(100, "TWD"), 1);
                dbContext.Orders.Add(order);
                orderIds.Add(order.Id.Value); orderToCustomer[order.Id.Value] = customerId;
            }
            await dbContext.SaveChangesAsync();
        }

        // 2. Act: Try to submit all 100 orders concurrently via the API
        var tasks = new List<Task<HttpResponseMessage>>();
        
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        

        // We use Task.Run to attempt to hit the controller simultaneously
        foreach (var orderId in orderIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Orders/{orderId}/submit");
                request.Headers.Add("X-Test-User-Id", orderToCustomer.ContainsKey(orderId) ? orderToCustomer[orderId].ToString() : Guid.NewGuid().ToString());
                return await client.SendAsync(request);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 3. Assert
        var successfulSubmissions = results.Count(r => r.IsSuccessStatusCode);
        
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var finalInventory = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductReference == productRef);
            
            // Output exactly what happened
            Console.WriteLine($"Successful submissions: {successfulSubmissions}");
            Console.WriteLine($"Final available stock: {finalInventory!.AvailableQuantity.Value}");
            
            // Stock must not drop below 0 (no overselling)
            finalInventory.AvailableQuantity.Value.Should().BeGreaterThanOrEqualTo(0);
            
            // In a perfectly resilient concurrency setup, exactly 10 should succeed.
            // But with optimistic locking, it's likely that < 10 succeed.
            successfulSubmissions.Should().Be(10, $"Expected exactly 10 successful submissions, but got {successfulSubmissions}");

            // Verify failed submissions remain Pending
            var allSubmittedOrderIds = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Submitted)
                .Select(o => o.Id.Value).ToListAsync();
            var submittedOrderIds = allSubmittedOrderIds.Where(id => orderIds.Contains(id)).ToList();
            submittedOrderIds.Count.Should().Be(10);
            
            var allPendingOrderIds = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Pending)
                .Select(o => o.Id.Value).ToListAsync();
            var pendingOrderIds = allPendingOrderIds.Where(id => orderIds.Contains(id)).ToList();
            pendingOrderIds.Count.Should().Be(90);
        }
    }

    [Fact]
    public async Task SubmitOrder_With20ConcurrentSubmissionsForStock1_Exactly1Succeeds()
    {
        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);
        int initialStock = 1;
        int concurrentCount = 20;

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var inventoryItem = InventoryItem.Create(productRef);
            inventoryItem.IncreaseStock(initialStock);
            dbContext.InventoryItems.Add(inventoryItem);
            await dbContext.SaveChangesAsync();
        }

        var orderIds = new List<Guid>();
        var orderToCustomer = new Dictionary<Guid, Guid>();

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            for (int i = 0; i < concurrentCount; i++)
            {
                var customerId = Guid.NewGuid();
                var order = Order.Create(customerId, "TWD");
                order.AddItem(new ProductId(productId), new Money(100, "TWD"), 1);
                dbContext.Orders.Add(order);
                orderIds.Add(order.Id.Value); orderToCustomer[order.Id.Value] = customerId;
            }
            await dbContext.SaveChangesAsync();
        }

        var tasks = new List<Task<HttpResponseMessage>>();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        

        foreach (var orderId in orderIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Orders/{orderId}/submit");
                request.Headers.Add("X-Test-User-Id", orderToCustomer.ContainsKey(orderId) ? orderToCustomer[orderId].ToString() : Guid.NewGuid().ToString());
                return await client.SendAsync(request);
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successfulSubmissions = results.Count(r => r.IsSuccessStatusCode);
        
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var finalInventory = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductReference == productRef);
            
            finalInventory!.AvailableQuantity.Value.Should().Be(0); // Never goes negative
            successfulSubmissions.Should().Be(1, $"Expected exactly 1 successful submissions, but got {successfulSubmissions}");

            var allPendingOrderIds = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Pending)
                .Select(o => o.Id.Value).ToListAsync();
            var pendingOrderIds = allPendingOrderIds.Where(id => orderIds.Contains(id)).ToList();
            pendingOrderIds.Count.Should().Be(19);
        }
    }

    [Fact]
    public async Task SubmitOrder_WithMultiItemOrder_AndInsufficientStockForOne_RollsBackEverything()
    {
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            
            var inv1 = InventoryItem.Create(new ProductReference(product1Id));
            inv1.IncreaseStock(10);
            var inv2 = InventoryItem.Create(new ProductReference(product2Id));
            inv2.IncreaseStock(0); // Insufficient stock
            
            dbContext.InventoryItems.AddRange(inv1, inv2);

            var order = Order.Create(customerId, "TWD");
            // Add custom order id to be able to use it
            var t = typeof(Order);
            var idProp = t.GetProperty("Id");
            idProp!.SetValue(order, new OrderId(orderId));

            order.AddItem(new ProductId(product1Id), new Money(100, "TWD"), 5);
            order.AddItem(new ProductId(product2Id), new Money(100, "TWD"), 5);
            
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
        

        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/submit", null);
        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var pRef1 = new ProductReference(product1Id);
            var inv1 = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductReference == pRef1);
            
            // Stock for product 1 should NOT be reserved since product 2 failed
            inv1!.AvailableQuantity.Value.Should().Be(10);

            var oId = new OrderId(orderId);
            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == oId);
            order!.Status.Should().Be(OrderStatus.Pending);
        }
    }

    [Fact]
    public async Task SubmitOrder_WithConcurrentMultiItemOrders_OverlappingSkus_DoesNotDeadlock()
    {
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            
            var inv1 = InventoryItem.Create(new ProductReference(product1Id));
            inv1.IncreaseStock(10);
            var inv2 = InventoryItem.Create(new ProductReference(product2Id));
            inv2.IncreaseStock(10);
            
            dbContext.InventoryItems.AddRange(inv1, inv2);
            await dbContext.SaveChangesAsync();
        }

        var orderIds = new List<Guid>();
        var orderToCustomer = new Dictionary<Guid, Guid>();

        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            for (int i = 0; i < 20; i++)
            {
                var customerId = Guid.NewGuid();
                var order = Order.Create(customerId, "TWD");
                
                // Add items in different orders to encourage deadlocks if locking order isn't deterministic
                if (i % 2 == 0)
                {
                    order.AddItem(new ProductId(product1Id), new Money(100, "TWD"), 1);
                    order.AddItem(new ProductId(product2Id), new Money(200, "TWD"), 1);
                }
                else
                {
                    order.AddItem(new ProductId(product2Id), new Money(200, "TWD"), 1);
                    order.AddItem(new ProductId(product1Id), new Money(100, "TWD"), 1);
                }
                
                dbContext.Orders.Add(order);
                orderIds.Add(order.Id.Value); orderToCustomer[order.Id.Value] = customerId;
            }
            await dbContext.SaveChangesAsync();
        }

        var tasks = new List<Task<HttpResponseMessage>>();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        

        foreach (var orderId in orderIds)
        {
            tasks.Add(Task.Run(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Orders/{orderId}/submit");
                request.Headers.Add("X-Test-User-Id", orderToCustomer.ContainsKey(orderId) ? orderToCustomer[orderId].ToString() : Guid.NewGuid().ToString());
                return await client.SendAsync(request);
            }));
        }

        var results = await Task.WhenAll(tasks);
        var successfulSubmissions = results.Count(r => r.IsSuccessStatusCode);
        
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var pRef1 = new ProductReference(product1Id);
            var pRef2 = new ProductReference(product2Id);
            var inv1 = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductReference == pRef1);
            var inv2 = await dbContext.InventoryItems.FirstOrDefaultAsync(i => i.ProductReference == pRef2);
            
            // Only 10 orders can succeed because stock is 10
            successfulSubmissions.Should().Be(10);
            inv1!.AvailableQuantity.Value.Should().Be(0);
            inv2!.AvailableQuantity.Value.Should().Be(0);
        }
    }
}
