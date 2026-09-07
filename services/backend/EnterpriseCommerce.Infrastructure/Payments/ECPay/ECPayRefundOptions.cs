namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

/// <summary>
/// 綠界金流退款與查詢專用組態選項。
/// 嚴格遵循安全原則，不提供任何生產環境預設 URL，任何缺失均在發起 HTTP 請求前 Fail-Closed。
/// </summary>
public sealed class ECPayRefundOptions
{
    public string? MerchantId { get; set; }
    public string? HashKey { get; set; }
    public string? HashIv { get; set; }
    public string? CreditCheckCode { get; set; }
    public string? QueryTradeUrl { get; set; }
    public string? DoActionUrl { get; set; }

    /// <summary>
    /// 驗證組態完整性。若有缺少或格式不合法，立即拋出異常以中斷執行，且不洩漏機密。
    /// </summary>
    public void Validate(bool requireDoActionUrl = false)
    {
        if (string.IsNullOrWhiteSpace(MerchantId))
        {
            throw new InvalidOperationException("ECPay MerchantId is not configured. Refund processing fails closed.");
        }

        if (string.IsNullOrWhiteSpace(HashKey))
        {
            throw new InvalidOperationException("ECPay HashKey is not configured. Refund processing fails closed.");
        }

        if (string.IsNullOrWhiteSpace(HashIv))
        {
            throw new InvalidOperationException("ECPay HashIv is not configured. Refund processing fails closed.");
        }

        if (string.IsNullOrWhiteSpace(CreditCheckCode))
        {
            throw new InvalidOperationException("ECPay CreditCheckCode is not configured. Refund processing fails closed.");
        }

        if (string.IsNullOrWhiteSpace(QueryTradeUrl) || !Uri.TryCreate(QueryTradeUrl, UriKind.Absolute, out var queryUri))
        {
            throw new InvalidOperationException("ECPay QueryTradeUrl must be a valid absolute URL.");
        }

        if (!queryUri.IsLoopback && queryUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("ECPay QueryTradeUrl must use HTTPS for non-loopback endpoints.");
        }

        if (requireDoActionUrl)
        {
            if (string.IsNullOrWhiteSpace(DoActionUrl) || !Uri.TryCreate(DoActionUrl, UriKind.Absolute, out var actionUri))
            {
                throw new InvalidOperationException("ECPay DoActionUrl must be a valid absolute URL.");
            }

            if (!actionUri.IsLoopback && actionUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("ECPay DoActionUrl must use HTTPS for non-loopback endpoints.");
            }
        }
    }
}
