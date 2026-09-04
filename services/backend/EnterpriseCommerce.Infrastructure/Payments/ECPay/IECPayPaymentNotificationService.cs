using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public interface IECPayPaymentNotificationService
{
    ProcessPaymentWebhookCommand? VerifyAndParseNotification(IReadOnlyDictionary<string, string> formFields);
}
