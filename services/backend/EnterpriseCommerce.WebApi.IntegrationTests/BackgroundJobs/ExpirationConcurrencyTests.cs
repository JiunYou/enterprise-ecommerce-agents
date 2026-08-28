using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Application.Orders.Commands.ExpireOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

namespace EnterpriseCommerce.WebApi.IntegrationTests.BackgroundJobs;

[Collection("IntegrationTests")]
public class ExpirationConcurrencyTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private IServiceScope? _scope;
    private EnterpriseCommerceDbContext? _dbContext;
    
    public ExpirationConcurrencyTests(MySqlFixture mySqlFixture)
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

        // Turn off automatic background worker so it doesn't interfere with manual concurrency tests

        
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

        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
    }

    public async Task DisposeAsync()
    {
        if (_scope != null) _scope.Dispose();
        if (_factory != null) await _factory.DisposeAsync();


    }

    private async Task<(Guid orderId, Guid productId)> SetupOrderAndInventory(int initialStock)
    {
        var productId = Guid.NewGuid();
        
        var inventoryItem = InventoryItem.Create(new ProductReference(productId));
        inventoryItem.IncreaseStock(initialStock);
        _dbContext!.InventoryItems.Add(inventoryItem);
        await _dbContext.SaveChangesAsync();

        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(productId), new Money(100, "TWD"), 1);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        return (order.Id.Value, productId);
    }

    private async Task SubmitOrderAsync(Guid orderId)
    {
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/submit", null);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Submit failed with {response.StatusCode}: {content}");
        }
    }

    [Fact]
    public async Task Scenario1_ExpiredSubmittedOrder_CancelsAndReleasesInventory()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        await SubmitOrderAsync(orderId);
        
        _dbContext!.ChangeTracker.Clear();
        
        // Time travel: make it expired by manually modifying SubmittedAt using raw SQL so we don't mess up concurrency token
        await _dbContext!.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET SubmittedAt = @p0 WHERE Id = @p1", 
            DateTimeOffset.UtcNow.AddMinutes(-30), orderId);

        // Expire it
        var sender = _scope!.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new ExpireOrderCommand(orderId));
        result.IsSuccess.Should().BeTrue();

        // Process Outbox
        var outboxMessages = await _dbContext.OutboxMessages.Where(m => m.ProcessedOn == null && m.Content.Contains(orderId.ToString())).ToListAsync();
        outboxMessages.Should().Contain(m => m.EventType == "OrderStatusChangedDomainEvent");
        
        // Outbox background service typically handles this. For the test, we'll dispatch directly or just check outbox
        var dispatcher = _scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var @event = System.Text.Json.JsonSerializer.Deserialize<EnterpriseCommerce.Domain.Orders.Events.OrderStatusChangedDomainEvent>(
            outboxMessages.First(m => m.EventType == "OrderStatusChangedDomainEvent" && m.Content.Contains("\"NewStatus\":4")).Content)!;
        await dispatcher.DispatchAsync(@event);

        var finalInventory = await _dbContext.InventoryItems.FirstAsync(i => i.ProductReference == new EnterpriseCommerce.Domain.Inventory.ValueObjects.ProductReference(productId));
        finalInventory.AvailableQuantity.Value.Should().Be(10);
        finalInventory.ReservedQuantity.Value.Should().Be(0);
        
        var order = await _dbContext.Orders.FindAsync(new OrderId(orderId));
        order!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Scenario2_PaidOrder_CannotBeExpired()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        await SubmitOrderAsync(orderId);
        
        _dbContext!.ChangeTracker.Clear();
        
        var order = await _dbContext!.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == new OrderId(orderId));
        var markResult = order!.MarkAsPaid();
        markResult.IsSuccess.Should().BeTrue();
        await _dbContext.SaveChangesAsync();

        var sender = _scope!.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new ExpireOrderCommand(orderId));
        
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Order.InvalidStatusTransition");
        
        _dbContext.ChangeTracker.Clear();
        order = await _dbContext.Orders.FindAsync(new OrderId(orderId));
        order!.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Scenario3_PaymentConfirmation_Vs_Expiration_Concurrency()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        await SubmitOrderAsync(orderId);

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET SubmittedAt = @p0 WHERE Id = @p1", 
            DateTimeOffset.UtcNow.AddMinutes(-30), orderId);

        // We run MarkAsPaid and ExpireOrderCommand concurrently in different scopes
        var tasks = new List<Task<bool>>();
        
        for (int i = 0; i < 2; i++)
        {
            var isPayment = i == 0;
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _factory!.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
                
                try 
                {
                    if (isPayment)
                    {
                        var o = await db.Orders.Include(order => order.Items).FirstOrDefaultAsync(order => order.Id == new OrderId(orderId));
                        var markResult = o!.MarkAsPaid();
                        if (markResult.IsFailure) return false;
                        await db.SaveChangesAsync();
                        return true;
                    }
                    else
                    {
                        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                        var res = await sender.Send(new ExpireOrderCommand(orderId));
                        return res.IsSuccess;
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        
        // Exactly one should succeed
        results.Count(r => r).Should().Be(1);

        _dbContext!.ChangeTracker.Clear();
        var finalOrder = await _dbContext!.Orders.FindAsync(new OrderId(orderId));
        finalOrder!.Status.Should().Match(s => s == OrderStatus.Paid || s == OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Scenario4_MultipleExpirationWorkers_TargetingSameOrder()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        await SubmitOrderAsync(orderId);

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "UPDATE Orders SET SubmittedAt = @p0 WHERE Id = @p1", 
            DateTimeOffset.UtcNow.AddMinutes(-30), orderId);

        var tasks = new List<Task<bool>>();
        
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _factory!.Services.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                try
                {
                    var res = await sender.Send(new ExpireOrderCommand(orderId));
                    return res.IsSuccess;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }));
        }

        var results = await Task.WhenAll(tasks);
        
        results.Count(r => r).Should().Be(1); // Exactly one worker cancels it successfully

        _dbContext!.ChangeTracker.Clear();
        var finalOrder = await _dbContext!.Orders.FindAsync(new OrderId(orderId));
        finalOrder!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Scenario6_SubmittedAt_UTC_Semantics()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        
        var order = await _dbContext!.Orders.FindAsync(new OrderId(orderId));
        order!.Status.Should().Be(OrderStatus.Pending);
        order.SubmittedAt.Should().BeNull();

        await SubmitOrderAsync(orderId);
        
        await _dbContext.Entry(order).ReloadAsync();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.SubmittedAt.Should().NotBeNull();
        order.SubmittedAt.Value.Offset.Should().Be(TimeSpan.Zero); // UTC check
    }

    [Fact]
    public async Task Scenario5_TransactionRollback_PreventsPartialState()
    {
        var (orderId, productId) = await SetupOrderAndInventory(10);
        await SubmitOrderAsync(orderId);

        // We simulate a failure by using a faulty transaction or intercepting.
        // Actually, we can just test that if CancelOrder throws, nothing is saved.
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.BeginTransactionAsync();
        var order = await db.Orders.FindAsync(new OrderId(orderId));
        order!.Cancel();
        
        // Save changes to database (but within uncommitted transaction)
        await db.SaveChangesAsync();
        
        // Simulate a crash before Commit
        db.ChangeTracker.Clear();
        await db.RollbackTransactionAsync();

        // New scope to verify
        using var newScope = _factory.Services.CreateScope();
        var newDb = newScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var finalOrder = await newDb.Orders.FindAsync(new OrderId(orderId));
        
        finalOrder!.Status.Should().Be(OrderStatus.Submitted);
        
        var outboxMessages = await newDb.OutboxMessages.Where(m => m.Content.Contains(orderId.ToString())).ToListAsync();
        outboxMessages.Should().NotContain(m => m.Content.Contains("\"NewStatus\":4")); // No cancelled event written
    }
}
