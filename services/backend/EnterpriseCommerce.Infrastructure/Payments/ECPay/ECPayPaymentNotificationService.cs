using System.Globalization;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public sealed class ECPayPaymentNotificationService : IECPayPaymentNotificationService
{
    private readonly ECPayPaymentOptions _options;

    public ECPayPaymentNotificationService(
        IOptions<ECPayPaymentOptions> options,
        IConfiguration configuration)
    {
        _options = options?.Value ?? new ECPayPaymentOptions();
        _options.MerchantId ??= configuration["Payments:ECPay:MerchantId"];
        _options.HashKey ??= configuration["Payments:ECPay:HashKey"];
        _options.HashIv ??= configuration["Payments:ECPay:HashIv"];
        _options.ReturnUrl ??= configuration["Payments:ECPay:ReturnUrl"];
    }

    public ProcessPaymentWebhookCommand? VerifyAndParseNotification(IReadOnlyDictionary<string, string> formFields)
    {
        ArgumentNullException.ThrowIfNull(formFields);

        if (string.IsNullOrWhiteSpace(_options.HashKey) || string.IsNullOrWhiteSpace(_options.HashIv))
        {
            throw new InvalidOperationException(
                "ECPay HashKey or HashIv is not configured. Webhook verification fails closed.");
        }

        if (string.IsNullOrWhiteSpace(_options.MerchantId))
        {
            throw new InvalidOperationException(
                "ECPay MerchantId is not configured. Webhook verification fails closed.");
        }

        // 1. 確保 CheckMacValue 欄位存在
        if (!formFields.TryGetValue("CheckMacValue", out var checkMacValue) || string.IsNullOrWhiteSpace(checkMacValue))
        {
            throw new ECPayNotificationValidationException("Missing required CheckMacValue.");
        }

        // 2. 驗證 CheckMacValue 密碼學真實性
        if (!ECPayCheckMacValue.Verify(formFields, _options.HashKey, _options.HashIv))
        {
            throw new ECPayNotificationValidationException("Invalid CheckMacValue signature.");
        }

        // 3. 驗證 MerchantID 與特店配置一致
        if (!formFields.TryGetValue("MerchantID", out var callbackMerchantId) ||
            !string.Equals(callbackMerchantId, _options.MerchantId, StringComparison.Ordinal))
        {
            throw new ECPayNotificationValidationException("MerchantID mismatch.");
        }

        // 4. SimulatePaid 檢查：若為模擬付款通知 (SimulatePaid == 1)，絕不派發支付成功 Command
        if (formFields.TryGetValue("SimulatePaid", out var simulatePaidStr) &&
            string.Equals(simulatePaidStr?.Trim(), "1", StringComparison.Ordinal))
        {
            // 模擬付款通知安全確認收到 (回傳 null，由端點回應 1|OK)，不對網域模型進行任何狀態變更
            return null;
        }

        // 5. RtnCode 檢查：若非成功狀態 (RtnCode != 1)，保守處理，不標記 Paid 亦不盲目標記 Failed
        if (!formFields.TryGetValue("RtnCode", out var rtnCodeStr) ||
            !string.Equals(rtnCodeStr?.Trim(), "1", StringComparison.Ordinal))
        {
            // 非成功狀態在 P3 遞延最終判定，回傳 null 以純文字 1|OK 確認收到
            return null;
        }

        // 6. 成功付款語意剖析：由 CustomField1 取出 PaymentAttemptId
        if (!formFields.TryGetValue("CustomField1", out var customField1) ||
            !Guid.TryParse(customField1, out var attemptGuid))
        {
            throw new ECPayNotificationValidationException("Invalid or missing CustomField1 (PaymentAttemptId).");
        }

        // 7. 取得第三方交易序號 TradeNo
        if (!formFields.TryGetValue("TradeNo", out var tradeNo) || string.IsNullOrWhiteSpace(tradeNo))
        {
            throw new ECPayNotificationValidationException("Missing required TradeNo for successful payment notification.");
        }

        // 8. 取得交易金額 TradeAmt (正整數)
        if (!formFields.TryGetValue("TradeAmt", out var tradeAmtStr) ||
            !long.TryParse(tradeAmtStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tradeAmt) ||
            tradeAmt <= 0)
        {
            throw new ECPayNotificationValidationException("Invalid TradeAmt in payment notification.");
        }

        return new ProcessPaymentWebhookCommand(
            PaymentAttemptId: new PaymentAttemptId(attemptGuid),
            Provider: "ECPay",
            ProviderEventId: tradeNo,
            ProviderTransactionId: tradeNo,
            Amount: (decimal)tradeAmt,
            Currency: "TWD",
            IsSuccess: true);
    }
}
