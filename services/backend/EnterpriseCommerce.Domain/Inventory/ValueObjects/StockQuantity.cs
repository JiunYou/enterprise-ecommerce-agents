using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.ValueObjects;

public sealed class StockQuantity : ValueObject
{
    public int Value { get; }

    public StockQuantity(int value)
    {
        if (value < 0)
        {
            throw new DomainException("StockQuantity cannot be negative.");
        }

        Value = value;
    }

    public static StockQuantity Zero => new(0);

    public StockQuantity Add(StockQuantity other)
    {
        return new StockQuantity(Value + other.Value);
    }

    public StockQuantity Subtract(StockQuantity other)
    {
        var newValue = Value - other.Value;
        if (newValue < 0)
        {
            throw new DomainException("Cannot subtract stock resulting in a negative quantity.");
        }

        return new StockQuantity(newValue);
    }

    public static StockQuantity operator +(StockQuantity first, StockQuantity second) => first.Add(second);
    public static StockQuantity operator -(StockQuantity first, StockQuantity second) => first.Subtract(second);
    
    public static bool operator >(StockQuantity first, StockQuantity second) => first.Value > second.Value;
    public static bool operator <(StockQuantity first, StockQuantity second) => first.Value < second.Value;
    public static bool operator >=(StockQuantity first, StockQuantity second) => first.Value >= second.Value;
    public static bool operator <=(StockQuantity first, StockQuantity second) => first.Value <= second.Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator int(StockQuantity quantity) => quantity.Value;
    public static implicit operator StockQuantity(int value) => new(value);
}
