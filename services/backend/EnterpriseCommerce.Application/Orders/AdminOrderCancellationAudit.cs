namespace EnterpriseCommerce.Application.Orders;

/// <summary>
/// 管理員訂單取消審計資料契約。
/// </summary>
public sealed record AdminOrderCancellationAudit(
    Guid OrderId,
    string ActorIssuer,
    string ActorSubject,
    DateTimeOffset CancelledAt,
    string Reason);
