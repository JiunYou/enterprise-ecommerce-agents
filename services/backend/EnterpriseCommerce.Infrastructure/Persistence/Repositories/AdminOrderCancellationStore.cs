using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence.Orders;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

/// <summary>
/// 管理員訂單取消審計儲存實作。
/// </summary>
internal sealed class AdminOrderCancellationStore : IAdminOrderCancellationStore
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public AdminOrderCancellationStore(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(AdminOrderCancellationAudit audit)
    {
        var entity = AdminOrderCancellation.Create(
            new OrderId(audit.OrderId),
            audit.ActorIssuer,
            audit.ActorSubject,
            audit.CancelledAt,
            audit.Reason);

        _dbContext.AdminOrderCancellations.Add(entity);
    }

    public async Task<AdminOrderCancellationAudit?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var orderIdVo = new OrderId(orderId);
        var entity = await _dbContext.AdminOrderCancellations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OrderId == orderIdVo, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new AdminOrderCancellationAudit(
            entity.OrderId.Value,
            entity.ActorIssuer,
            entity.ActorSubject,
            entity.CancelledAt,
            entity.Reason);
    }
}
