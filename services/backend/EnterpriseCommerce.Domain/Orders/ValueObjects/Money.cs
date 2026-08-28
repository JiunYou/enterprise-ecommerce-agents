using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new DomainException("Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("Currency is required.");
        }

        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new DomainException("Cannot add different currencies.");
        }

        return new Money(Amount + other.Amount, Currency);
    }

    public static Money operator +(Money first, Money second)
    {
        return first.Add(second);
    }

    public Money Multiply(int multiplier)
    {
        if (multiplier < 0)
        {
            throw new DomainException("Multiplier cannot be negative.");
        }

        return new Money(Amount * multiplier, Currency);
    }

    public static Money operator *(Money money, int multiplier)
    {
        return money.Multiply(multiplier);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
