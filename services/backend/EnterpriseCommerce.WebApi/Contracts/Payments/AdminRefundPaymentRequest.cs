namespace EnterpriseCommerce.WebApi.Contracts.Payments;

/// <summary>
/// Admin 退款請求合約。
/// 僅允許 Reason 欄位，嚴禁由客戶端提供任何金額、幣別、金流商、交易號或身分資訊。
/// </summary>
public sealed record AdminRefundPaymentRequest(string? Reason);
