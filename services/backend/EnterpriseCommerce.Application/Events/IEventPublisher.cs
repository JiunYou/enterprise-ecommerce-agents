namespace EnterpriseCommerce.Application.Events;

public interface IEventPublisher
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default);
}
