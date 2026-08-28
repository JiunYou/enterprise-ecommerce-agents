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

public class OutboxBackgroundServiceTests
{
    private class FakeDomainEventDispatcher : IDomainEventDispatcher
    {
        public bool IsDispatched { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("In-process dispatch failed.");
            }
            IsDispatched = true;
            return Task.CompletedTask;
        }
    }

    private class FakeEventPublisher : IEventPublisher
    {
        public bool IsPublished { get; private set; }
        public bool ShouldThrow { get; set; }

        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("External broker unreachable.");
            }
            IsPublished = true;
            return Task.CompletedTask;
        }
    }

    private (ServiceProvider Provider, EnterpriseCommerceDbContext DbContext, FakeDomainEventDispatcher Dispatcher, FakeEventPublisher? Publisher) SetupEnvironment(bool registerPublisher = true, bool publisherThrows = false, bool dispatcherThrows = false)
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
    public async Task ProcessOutboxMessagesAsync_ShouldMarkProcessed_WhenDispatchAndPublishSucceed()
    {
        // Arrange
        var (provider, dbContext, dispatcher, publisher) = SetupEnvironment(registerPublisher: true);
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

        // Assert
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);
        updated!.ProcessedOn.Should().NotBeNull();
        updated.Error.Should().BeNull();
        dispatcher.IsDispatched.Should().BeTrue();
        publisher!.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldNotMarkProcessed_WhenEventPublisherNotRegistered()
    {
        // Arrange: External publisher is missing
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

        // Assert
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);
        updated!.ProcessedOn.Should().BeNull("Message must NOT be marked processed when external publisher is missing");
        updated.Error.Should().Contain("IEventPublisher is mandatory for publishing external event");
    }

    [Fact]
    public async Task ProcessOutboxMessagesAsync_ShouldNotMarkProcessed_WhenEventPublisherFails()
    {
        // Arrange: External publisher throws
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

        // Assert
        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        var updated = await verifyDb.OutboxMessages.FindAsync(outboxMessage.Id);
        updated!.ProcessedOn.Should().BeNull("Message must NOT be marked processed when external publish fails");
        updated.Error.Should().Contain("External broker unreachable");
    }
}
