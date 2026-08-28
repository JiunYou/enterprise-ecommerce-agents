using System.Reflection;
using System.Text.Json;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EnterpriseCommerce.Infrastructure.Persistence.Outbox;

public class OutboxBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(IServiceProvider serviceProvider, ILogger<OutboxBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            // Poll every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
            var domainEventDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            var integrationEventMapper = scope.ServiceProvider.GetService<IIntegrationEventMapper>();
            var eventPublisher = scope.ServiceProvider.GetService<IEventPublisher>();

            var messages = await dbContext.OutboxMessages
                .Where(m => m.ProcessedOn == null)
                .OrderBy(m => m.OccurredOn)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    var assembly = Assembly.GetAssembly(typeof(DomainEvent));
                    var eventType = assembly?.GetType($"EnterpriseCommerce.Domain.Orders.Events.{message.EventType}") 
                                 ?? assembly?.GetType($"EnterpriseCommerce.Domain.Inventory.Events.{message.EventType}")
                                 ?? assembly?.GetType($"EnterpriseCommerce.Domain.Catalog.Events.{message.EventType}");
                    
                    if (eventType != null)
                    {
                        var domainEvent = JsonSerializer.Deserialize(message.Content, eventType) as DomainEvent;
                        if (domainEvent != null)
                        {
                            // 1. Dispatch synchronously to in-process domain event handlers
                            await domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);

                            // 2. Publish to external event broker if integration event is mapped
                            if (integrationEventMapper != null)
                            {
                                var envelope = integrationEventMapper.MapFrom(domainEvent);
                                if (envelope != null)
                                {
                                    if (eventPublisher == null)
                                    {
                                        throw new InvalidOperationException($"IEventPublisher is mandatory for publishing external event '{message.EventType}' but no implementation is registered in the service provider.");
                                    }

                                    await eventPublisher.PublishAsync(envelope, cancellationToken);
                                }
                            }
                        }
                    }

                    message.ProcessedOn = DateTime.UtcNow;
                    message.Error = null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox message {MessageId}. It will be retried.", message.Id);
                    message.Error = ex.ToString();
                    // We DO NOT set ProcessedOn here, allowing the worker to pick it up again in the next polling cycle.
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages");
        }
    }
}
