using System.Globalization;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.Extensions.Options;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public sealed class ECPayPaymentProvider : IPaymentProvider
{
    private readonly ECPayPaymentOptions _options;

    public string ProviderName => "ECPay";

    public ECPayPaymentProvider(IOptions<ECPayPaymentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public ECPayPaymentProvider(ECPayPaymentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public Task<InitiatePaymentResponse> InitiatePaymentAsync(
        PaymentAttemptId paymentAttemptId,
        OrderId orderId,
        decimal amount,
        string currency,
        DateTimeOffset paymentAttemptCreatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentAttemptId);
        ArgumentNullException.ThrowIfNull(orderId);

        _options.Validate();

        // 貨幣驗證：僅接受 TWD
        if (!string.Equals(currency?.Trim(), "TWD", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"ECPay provider only supports TWD currency. Provided currency: '{currency}'.");
        }

        // 金額驗證：必須為正整數
        if (amount <= 0 || decimal.Truncate(amount) != amount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "ECPay TotalAmount must be a positive whole integer without fractional units.");
        }

        // MerchantTradeDate：以 PaymentAttempt.CreatedAt 衍生，固定台灣時區 (UTC+08:00)，格式 yyyy/MM/dd HH:mm:ss
        var taiwanOffset = TimeSpan.FromHours(8);
        var taiwanTime = paymentAttemptCreatedAt.ToOffset(taiwanOffset);
        var tradeDate = taiwanTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);

        // MerchantTradeNo：<= 20 字元確定性衍生
        var merchantTradeNo = ECPayMerchantTradeNo.FromPaymentAttemptId(paymentAttemptId);
        var clientBackUrl = $"{_options.ClientBackUrlBase!.TrimEnd('/')}/orders/{orderId.Value}?payment=returned";

        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = _options.MerchantId!,
            ["MerchantTradeNo"] = merchantTradeNo,
            ["MerchantTradeDate"] = tradeDate,
            ["PaymentType"] = "aio",
            ["TotalAmount"] = ((long)amount).ToString(CultureInfo.InvariantCulture),
            ["TradeDesc"] = "EnterpriseCommerce Order",
            ["ItemName"] = "EnterpriseCommerce Order",
            ["ReturnURL"] = _options.ReturnUrl!,
            ["ChoosePayment"] = "Credit",
            ["ClientBackURL"] = clientBackUrl,
            ["EncryptType"] = "1",
            ["CustomField1"] = paymentAttemptId.Value.ToString("N"),
            ["CustomField2"] = orderId.Value.ToString("N")
        };

        var checkMacValue = ECPayCheckMacValue.Generate(fields, _options.HashKey!, _options.HashIv!);
        fields["CheckMacValue"] = checkMacValue;

        var response = new InitiatePaymentResponse(
            ProviderTransactionId: null,
            ActionUrl: _options.ActionUrl,
            Method: PaymentLaunchMethod.Post,
            FormFields: fields);

        return Task.FromResult(response);
    }
}
