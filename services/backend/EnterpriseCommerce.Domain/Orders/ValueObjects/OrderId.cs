using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders.ValueObjects;

public sealed class OrderId : ValueObject
{
    public Guid Value { get; }

    public OrderId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("OrderId cannot be empty.");
        }
        
        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(OrderId orderId) => orderId.Value;
    public static implicit operator OrderId(Guid value) => new(value);
}
