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

    public async Task<IReadOnlyList<Order>> GetFulfillmentQueueAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Paid)
            .OrderBy(o => o.SubmittedAt)
            .ThenBy(o => o.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetAdminOrdersAsync(
        OrderStatus? status,
        OrderId? orderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (orderId is not null)
        {
            query = query.Where(o => o.Id == orderId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.SubmittedAt)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
