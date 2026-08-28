using System.Text.Json;
using EnterpriseCommerce.Application.Events;

namespace EnterpriseCommerce.Infrastructure.Messaging;

public class EventSerializer : IEventSerializer
{
    public byte[] SerializeToBytes(EventEnvelope envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }
}
