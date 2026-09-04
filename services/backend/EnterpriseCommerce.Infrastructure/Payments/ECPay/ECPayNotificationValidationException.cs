namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public class ECPayNotificationValidationException : Exception
{
    public ECPayNotificationValidationException(string message) : base(message)
    {
    }

    public ECPayNotificationValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
