using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Messaging;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Messaging;

public class RabbitMqEventPublisherTests
{
    private class FakeConnectionFactory : IRabbitMqConnectionFactory
    {
        public bool IsChannelCreated { get; private set; }
        
        public object CreateChannel()
        {
            IsChannelCreated = true;
            return new object();
        }
    }

    private class FakeRetryPolicy : IRetryPolicy
    {
        public int ExecutionCount { get; private set; }
        
        public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            await action();
        }
    }

    [Fact]
    public async Task PublishAsync_ShouldUseRetryPolicyAndConnectionFactory()
    {
        // Arrange
        var connectionFactory = new FakeConnectionFactory();
        var serializer = new EventSerializer();
        var retryPolicy = new FakeRetryPolicy();
        var publisher = new RabbitMqEventPublisher(connectionFactory, serializer, retryPolicy);
        
        var envelope = new EventEnvelope(Guid.NewGuid(), "Type", 1, DateTimeOffset.UtcNow, "{}");

        // Act
        await publisher.PublishAsync(envelope);

        // Assert
        Assert.Equal(1, retryPolicy.ExecutionCount);
        Assert.True(connectionFactory.IsChannelCreated);
    }
}
