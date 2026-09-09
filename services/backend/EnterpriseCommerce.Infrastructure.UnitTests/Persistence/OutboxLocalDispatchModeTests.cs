using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence;

public class OutboxLocalDispatchModeTests
{
    private class FakeDomainEventDispatcher : IDomainEventDispatcher
    {
        public int DispatchCount { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            if (ShouldThrow)
            {
                throw new InvalidOperationException("In-process dispatch failed.");
            }
            return Task.CompletedTask;
        }
    }

    private class FakeEventPublisher : IEventPublisher
    {
        public int PublishCount { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            if (ShouldThrow)
            {
                throw new InvalidOperationException("External broker unreachable (throwing publisher).");
            }
            return Task.CompletedTask;
        }
    }

    private (ServiceProvider Provider, EnterpriseCommerceDbContext DbContext, FakeDomainEventDispatcher Dispatcher, FakeEventPublisher? Publisher) SetupEnvironment(
        bool registerPublisher = true, 
        bool publisherThrows = false, 
        bool dispatcherThrows = false)
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<EnterpriseCommerceDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var dispatcher = new FakeDomainEventDispatcher { ShouldThrow = dispatcherThrows };
        services.AddSingleton<IDomainEventDispatcher>(dispatcher);
        services.AddSingleton<IIntegrationEventMapper, IntegrationEventMapper>();

        FakeEventPublisher? publisher = null;
        if (registerPublisher)
        {
            publisher = new FakeEventPublisher { ShouldThrow = publisherThrows };
            services.AddSingleton<IEventPublisher>(publisher);
        }

        services.AddLogging();
        services.AddSingleton<ILogger<OutboxBackgroundService>>(NullLogger<OutboxBackgroundService>.Instance);

        var provider = services.BuildServiceProvider();
        var dbContext = provider.GetRequiredService<EnterpriseCommerceDbContext>();

        return (provider, dbContext, dispatcher, publisher);
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenNoPublisherRegistered_ShouldDispatchLocallyAndMarkProcessed()
    {
        // Arrange
        var (provider, dbContext, dispatcher, publisher) = SetupEnvironment(registerPublisher: false);
        var orderId = Guid.NewGuid();
        var domainEvent = new OrderCreatedDomainEvent(new OrderId(orderId), Guid.NewGuid());
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            EventType = nameof(OrderCreatedDomainEvent),
            Content = JsonSerializer.Serialize(domainEvent)
        };
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var service = new OutboxBackgroundService(provider, NullLogger<OutboxBackgroundService>.Instance);

        // Act
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Future Local-Dispatch-Only Contract
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);

        dispatcher.DispatchCount.Should().Be(1);
        updated!.ProcessedOn.Should().NotBeNull("Future local-dispatch-only contract must mark message processed even when no publisher is registered");
        updated.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenPublisherRegisteredAndThrows_ShouldIgnorePublisherAndMarkProcessed()
    {
        // Arrange: Publisher is registered but throws if invoked
        var (provider, dbContext, dispatcher, publisher) = SetupEnvironment(registerPublisher: true, publisherThrows: true);
        var orderId = Guid.NewGuid();
        var domainEvent = new OrderCreatedDomainEvent(new OrderId(orderId), Guid.NewGuid());
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            EventType = nameof(OrderCreatedDomainEvent),
            Content = JsonSerializer.Serialize(domainEvent)
        };
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var service = new OutboxBackgroundService(provider, NullLogger<OutboxBackgroundService>.Instance);

        // Act
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Future Local-Dispatch-Only Contract
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);

        dispatcher.DispatchCount.Should().Be(1);
        publisher!.PublishCount.Should().Be(0, "Future local-dispatch-only contract must never invoke external publisher");
        updated!.ProcessedOn.Should().NotBeNull("Future local-dispatch-only contract must mark message processed ignoring external publisher");
        updated.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenMessageSuccessfullyProcessed_ShouldNotRedispatchOnSubsequentRun()
    {
        // Arrange
        var (provider, dbContext, dispatcher, publisher) = SetupEnvironment(registerPublisher: false);
        var orderId = Guid.NewGuid();
        var domainEvent = new OrderCreatedDomainEvent(new OrderId(orderId), Guid.NewGuid());
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            EventType = nameof(OrderCreatedDomainEvent),
            Content = JsonSerializer.Serialize(domainEvent)
        };
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var service = new OutboxBackgroundService(provider, NullLogger<OutboxBackgroundService>.Instance);
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act - Run 1
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Act - Run 2
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Future Local-Dispatch-Only Contract
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);

        dispatcher.DispatchCount.Should().Be(1, "Successfully processed message must not be redispatched on subsequent runs");
        updated!.ProcessedOn.Should().NotBeNull();
        updated.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_WhenLocalDispatcherThrows_ShouldKeepMessageUnprocessedForRetry()
    {
        // Arrange: Local dispatcher throws
        var (provider, dbContext, dispatcher, publisher) = SetupEnvironment(registerPublisher: false, dispatcherThrows: true);
        var orderId = Guid.NewGuid();
        var domainEvent = new OrderCreatedDomainEvent(new OrderId(orderId), Guid.NewGuid());
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            EventType = nameof(OrderCreatedDomainEvent),
            Content = JsonSerializer.Serialize(domainEvent)
        };
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var service = new OutboxBackgroundService(provider, NullLogger<OutboxBackgroundService>.Instance);

        // Act
        var method = typeof(OutboxBackgroundService).GetMethod("ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // Assert - Characterization: Local dispatch failure must keep message unprocessed and record error
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);

        dispatcher.DispatchCount.Should().Be(1);
        updated!.ProcessedOn.Should().BeNull("Message must remain unprocessed when local dispatcher fails");
        updated.Error.Should().NotBeNull();
        updated.Error.Should().Contain("In-process dispatch failed");
    }
}
