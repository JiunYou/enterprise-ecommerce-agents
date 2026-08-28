namespace EnterpriseCommerce.Domain.Payments;

public enum PaymentAttemptStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    RefundRequired = 4
}
