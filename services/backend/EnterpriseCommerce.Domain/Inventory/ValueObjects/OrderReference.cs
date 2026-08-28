using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.ValueObjects;

public sealed class OrderReference : ValueObject
{
    public Guid Value { get; }

    public OrderReference(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("OrderReference cannot be empty.");
        }

        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(OrderReference reference) => reference.Value;
    public static implicit operator OrderReference(Guid value) => new(value);
}
