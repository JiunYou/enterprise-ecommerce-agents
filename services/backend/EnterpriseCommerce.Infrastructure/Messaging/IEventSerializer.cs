using EnterpriseCommerce.Application.Events;

namespace EnterpriseCommerce.Infrastructure.Messaging;

public interface IEventSerializer
{
    byte[] SerializeToBytes(EventEnvelope envelope);
}
