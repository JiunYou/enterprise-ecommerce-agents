using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public OrderRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Order order)
    {
        _dbContext.Orders.Add(order);
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByIdForUpdateAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        // Requires a transaction to be active.
        var sql = $"SELECT * FROM Orders WHERE Id = '{id.Value}' FOR UPDATE";
        
        // EF Core 8+ syntax for raw SQL on DbSet
        return await _dbContext.Orders
            .FromSqlRaw(sql)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Order?> GetPendingOrderByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending, cancellationToken);
    }
}
