using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests;

public class DomainEventTests
{
    private record TestDomainEvent : DomainEvent;

    [Fact]
    public void DomainEvent_ShouldBeCreatedWithIdAndTimestamp()
    {
        // Act
        var domainEvent = new TestDomainEvent();

        // Assert
        Assert.NotEqual(Guid.Empty, domainEvent.Id);
        Assert.True(domainEvent.OccurredOn <= DateTime.UtcNow);
        Assert.True(domainEvent.OccurredOn > DateTime.UtcNow.AddMinutes(-1));
    }
}
