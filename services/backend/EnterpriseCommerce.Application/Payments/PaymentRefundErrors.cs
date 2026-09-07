using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Payments;

public static class PaymentRefundErrors
{
    public static readonly Error RefundOrderPaymentInvariantViolation = new(
        "Payment.RefundOrderPaymentInvariantViolation",
        "Refund order payment invariant violation.");

    public static readonly Error RefundAlreadyExists = new(
        "Payment.RefundAlreadyExists",
        "A refund record already exists for this payment attempt.");

    public static readonly Error RefundNotEligible = new(
        "Payment.RefundNotEligible",
        "The payment attempt is not eligible for refund.");

    public static readonly Error RefundRequiredStatusExpected = new(
        "Payment.RefundRequiredStatusExpected",
        "Only payment attempts in RefundRequired status may be refunded.");

    public static readonly Error MissingProviderTransactionId = new(
        "Payment.MissingProviderTransactionId",
        "The payment attempt does not have a valid provider transaction ID.");
}
