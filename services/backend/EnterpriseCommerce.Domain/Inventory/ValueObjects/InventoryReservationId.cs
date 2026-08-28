using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.ValueObjects;

public sealed class InventoryReservationId : ValueObject
{
    public Guid Value { get; }

    public InventoryReservationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("InventoryReservationId cannot be empty.");
        }

        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(InventoryReservationId id) => id.Value;
    public static implicit operator InventoryReservationId(Guid value) => new(value);
}
