using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Payments.ValueObjects;

public sealed class PaymentAttemptId : ValueObject
{
    public Guid Value { get; }

    public PaymentAttemptId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("PaymentAttemptId cannot be empty.");
        }
        
        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator Guid(PaymentAttemptId paymentAttemptId) => paymentAttemptId.Value;
    public static implicit operator PaymentAttemptId(Guid value) => new(value);
}
