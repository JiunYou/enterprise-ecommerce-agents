namespace EnterpriseCommerce.Domain.Primitives;

public abstract record DomainEvent(Guid Id, DateTime OccurredOn) : IDomainEvent
{
    protected DomainEvent() : this(Guid.NewGuid(), DateTime.UtcNow)
    {
    }
}
