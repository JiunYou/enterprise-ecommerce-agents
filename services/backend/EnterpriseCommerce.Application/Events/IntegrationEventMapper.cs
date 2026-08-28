using System.Text.Json;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Events;

public class IntegrationEventMapper : IIntegrationEventMapper
{
    public EventEnvelope MapFrom(DomainEvent domainEvent)
    {
        var payload = JsonSerializer.Serialize((object)domainEvent);
        var eventType = domainEvent.GetType().Name;

        return new EventEnvelope(
            EventId: domainEvent.Id,
            EventType: eventType,
            Version: 1,
            OccurredAt: domainEvent.OccurredOn,
            Payload: payload
        );
    }
}
