namespace EnterpriseCommerce.WebApi.Contracts.Orders;

/// <summary>
/// 管理員取消訂單請求。
/// </summary>
public sealed record AdminCancelOrderRequest(string Reason);
