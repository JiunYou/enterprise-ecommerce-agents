using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
