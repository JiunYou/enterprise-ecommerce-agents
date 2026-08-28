using EnterpriseCommerce.Application.Events;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

public class TestEventPublisher : IEventPublisher
{
    public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
