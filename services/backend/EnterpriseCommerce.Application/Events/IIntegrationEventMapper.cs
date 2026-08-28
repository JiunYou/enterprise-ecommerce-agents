using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Events;

public interface IIntegrationEventMapper
{
    EventEnvelope MapFrom(DomainEvent domainEvent);
}
