namespace EnterpriseCommerce.Infrastructure.Messaging;

public interface IRabbitMqConnectionFactory
{
    // A placeholder for obtaining a channel/connection to RabbitMQ without tightly coupling to the underlying client library implementation in this domain logic
    object CreateChannel();
}
