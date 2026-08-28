using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Events;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}
