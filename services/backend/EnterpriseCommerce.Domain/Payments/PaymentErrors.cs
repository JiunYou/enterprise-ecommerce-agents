using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Payments;

public static class PaymentErrors
{
    public static readonly Error NotFound = new(
        "Payment.NotFound",
        "The payment attempt was not found.");

    public static readonly Error InvalidStatusTransition = new(
        "Payment.InvalidStatusTransition",
        "The requested status transition is not allowed.");

    public static readonly Error AmountMismatch = new(
        "Payment.AmountMismatch",
        "The provided amount does not match the payment attempt.");

    public static readonly Error CurrencyMismatch = new(
        "Payment.CurrencyMismatch",
        "The provided currency does not match the payment attempt.");
        
    public static readonly Error ConcurrentInitiation = new(
        "Payment.ConcurrentInitiation",
        "An active payment attempt already exists for this order.");

    public static readonly Error DuplicateTransactionIdMismatch = new(
        "Payment.DuplicateTransactionIdMismatch",
        "A duplicate webhook was received but the semantic payment data does not match the finalized authoritative state.");

    public static readonly Error ProviderMismatch = new(
        "Payment.ProviderMismatch",
        "The webhook provider does not match the payment attempt provider.");
}
