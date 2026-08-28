using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.ValueObjects;

public sealed class ProductReference : ValueObject
{
    public Guid Value { get; }

    public ProductReference(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("ProductReference cannot be empty.");
        }

        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(ProductReference reference) => reference.Value;
    public static implicit operator ProductReference(Guid value) => new(value);
}
