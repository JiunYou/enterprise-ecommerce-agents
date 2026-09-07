namespace EnterpriseCommerce.WebApi.Contracts.Payments;

/// <summary>
/// Admin 退款操作響應。
/// 僅包含中立結果語意，絕不暴露金流商原始內部狀態或交易號。
/// </summary>
public sealed record AdminRefundPaymentResponse(
    Guid PaymentAttemptId,
    string Outcome,
    string? RefundStatus);
