namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;

/// <summary>
/// Admin 訂單詳情中的 RefundRequired 付款狀態讀取模型。
/// 嚴禁暴露金流商敏感憑證與內部交易標識。
/// </summary>
public sealed record AdminRefundRequiredPaymentResponse(
    Guid PaymentAttemptId,
    decimal Amount,
    string Currency,
    string RefundCapability,
    string? RefundStatus);
