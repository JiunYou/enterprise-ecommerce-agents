using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseCommerce.Application.Events;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public DomainEventDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        // Get all registered handlers for this event type
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler == null) continue;

            var method = handlerType.GetMethod("HandleAsync");
            if (method != null)
            {
                var task = (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
                await task.ConfigureAwait(false);
            }
        }
    }
}
