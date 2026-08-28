using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Messaging;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Messaging;

public class EventSerializerTests
{

    [Fact]
    public void SerializeToBytes_ShouldReturnValidBytes()
    {
        // Arrange
        var serializer = new EventSerializer();
        var envelope = new EventEnvelope(Guid.NewGuid(), "Type", 1, DateTimeOffset.UtcNow, "Payload");

        // Act
        var bytes = serializer.SerializeToBytes(envelope);

        // Assert
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}
