using EnterpriseCommerce.Application.Events;

namespace EnterpriseCommerce.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IRabbitMqConnectionFactory _connectionFactory;
    private readonly IEventSerializer _serializer;
    private readonly IRetryPolicy _retryPolicy;

    public RabbitMqEventPublisher(
        IRabbitMqConnectionFactory connectionFactory,
        IEventSerializer serializer,
        IRetryPolicy retryPolicy)
    {
        _connectionFactory = connectionFactory;
        _serializer = serializer;
        _retryPolicy = retryPolicy;
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await _retryPolicy.ExecuteAsync(() =>
        {
            var channel = _connectionFactory.CreateChannel();
            var body = _serializer.SerializeToBytes(envelope);

            // Simulating basic publish logic
            // channel.BasicPublish(exchange: "domain-events", routingKey: envelope.EventType, basicProperties: null, body: body);

            return Task.CompletedTask;
        }, cancellationToken);
    }
}
