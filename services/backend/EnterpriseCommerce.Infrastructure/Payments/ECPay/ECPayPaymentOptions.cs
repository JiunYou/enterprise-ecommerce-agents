namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public sealed class ECPayPaymentOptions
{
    public const string DefaultStageActionUrl = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

    public string? MerchantId { get; set; }
    public string? HashKey { get; set; }
    public string? HashIv { get; set; }
    public string? ReturnUrl { get; set; }
    public string? ClientBackUrlBase { get; set; }
    public string ActionUrl { get; set; } = DefaultStageActionUrl;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(MerchantId))
        {
            throw new InvalidOperationException("ECPay MerchantId is not configured. Payment initiation fails closed.");
        }

        if (string.IsNullOrWhiteSpace(HashKey))
        {
            throw new InvalidOperationException("ECPay HashKey is not configured. Payment initiation fails closed.");
        }

        if (string.IsNullOrWhiteSpace(HashIv))
        {
            throw new InvalidOperationException("ECPay HashIv is not configured. Payment initiation fails closed.");
        }

        if (string.IsNullOrWhiteSpace(ReturnUrl))
        {
            throw new InvalidOperationException("ECPay ReturnUrl is not configured. Payment initiation fails closed.");
        }

        if (string.IsNullOrWhiteSpace(ClientBackUrlBase) || !Uri.TryCreate(ClientBackUrlBase, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("ECPay ClientBackUrlBase is not configured or invalid. Payment initiation fails closed.");
        }

        if (string.IsNullOrWhiteSpace(ActionUrl) || !Uri.TryCreate(ActionUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("ECPay ActionUrl must be a valid absolute HTTPS URL.");
        }
    }
}
