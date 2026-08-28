using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.ValueObjects;

public sealed class InventoryId : ValueObject
{
    public Guid Value { get; }

    public InventoryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("InventoryId cannot be empty.");
        }

        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(InventoryId id) => id.Value;
    public static implicit operator InventoryId(Guid value) => new(value);
}
