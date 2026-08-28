using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.UnitTests.Events;

public class IntegrationEventMapperTests
{
    private sealed record TestDomainEvent(string Data) : DomainEvent;

    [Fact]
    public void MapFrom_ShouldPreserveEventIdentityAndOccurredAt()
    {
        // Arrange
        var mapper = new IntegrationEventMapper();
        var domainEvent = new TestDomainEvent("Integration Test");

        // Act
        var envelope = mapper.MapFrom(domainEvent);

        // Assert
        Assert.Equal(domainEvent.Id, envelope.EventId); // CRITICAL: Identity stability
        Assert.Equal(domainEvent.OccurredOn, envelope.OccurredAt);
        Assert.Equal("TestDomainEvent", envelope.EventType);
        Assert.Equal(1, envelope.Version);
        Assert.Contains("Integration Test", envelope.Payload);
    }
}
