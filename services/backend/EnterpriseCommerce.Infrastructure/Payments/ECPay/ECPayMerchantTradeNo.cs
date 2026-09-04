using System.Security.Cryptography;
using System.Text;
using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public static class ECPayMerchantTradeNo
{
    public static string FromPaymentAttemptId(PaymentAttemptId paymentAttemptId)
    {
        ArgumentNullException.ThrowIfNull(paymentAttemptId);

        var bytes = Encoding.UTF8.GetBytes(paymentAttemptId.Value.ToString("N"));
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..20].ToUpperInvariant();
    }
}
