namespace EnterpriseCommerce.Application.Events;

public record EventEnvelope(
    Guid EventId,
    string EventType,
    int Version,
    DateTimeOffset OccurredAt,
    string Payload
);
