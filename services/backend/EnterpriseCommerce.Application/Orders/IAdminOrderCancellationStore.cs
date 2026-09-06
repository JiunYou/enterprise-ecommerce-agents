namespace EnterpriseCommerce.Application.Orders;

/// <summary>
/// 管理員訂單取消審計儲存抽象介面。
/// </summary>
public interface IAdminOrderCancellationStore
{
    void Add(AdminOrderCancellationAudit audit);

    Task<AdminOrderCancellationAudit?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);
}
