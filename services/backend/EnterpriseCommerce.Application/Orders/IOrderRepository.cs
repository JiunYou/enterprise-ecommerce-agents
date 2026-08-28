using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;

namespace EnterpriseCommerce.Application.Orders;

public interface IOrderRepository
{
    void Add(Order order);
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForUpdateAsync(OrderId id, CancellationToken cancellationToken = default);
}

